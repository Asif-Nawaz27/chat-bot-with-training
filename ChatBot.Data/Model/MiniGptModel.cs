using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace ChatBot.Data.Model;

/// <summary>
/// A small decoder-only Transformer ("mini-GPT"), trained with next-token
/// prediction: given a sequence of tokens, predict what the next token should be.
///
/// The overall flow is:
///   token ids -> token embeddings + positional embeddings -> N transformer
///   blocks -> final layer norm -> linear projection to vocabulary logits
///
/// The output logits are a score for every character in the vocabulary at
/// every position, representing "how likely is each character to come next".
/// </summary>
public class MiniGptModel : Module<Tensor, Tensor>
{
    private readonly GptConfig _config;
    private readonly Embedding _tokenEmbedding;
    private readonly Embedding _positionEmbedding;
    private readonly Dropout _embeddingDropout;
    private readonly ModuleList<TransformerBlock> _blocks;
    private readonly LayerNorm _finalNorm;
    private readonly Linear _lmHead;

    public MiniGptModel(GptConfig config) : base(nameof(MiniGptModel))
    {
        _config = config;

        // Token embedding: looks up a learned vector for each character id.
        _tokenEmbedding = Embedding(config.VocabSize, config.EmbedDim);

        // Positional embedding: a learned vector for each position (0, 1, 2, ...)
        // in the sequence. Attention has no built-in sense of order (it treats the
        // input as a set), so we add positional information explicitly.
        _positionEmbedding = Embedding(config.BlockSize, config.EmbedDim);

        _embeddingDropout = Dropout(config.Dropout);

        _blocks = new ModuleList<TransformerBlock>();
        for (int i = 0; i < config.NumLayers; i++)
        {
            _blocks.Add(new TransformerBlock(config));
        }

        _finalNorm = LayerNorm(config.EmbedDim);

        // Final projection from embedding space to a score per vocabulary entry.
        _lmHead = Linear(config.EmbedDim, config.VocabSize);

        RegisterComponents();
    }

    /// <summary>
    /// Runs the model on a batch of token id sequences.
    /// </summary>
    /// <param name="tokenIds">Shape (batch, sequenceLength), dtype long.</param>
    /// <returns>Logits of shape (batch, sequenceLength, vocabSize).</returns>
    public override Tensor forward(Tensor tokenIds)
    {
        var sequenceLength = (int)tokenIds.shape[1];
        if (sequenceLength > _config.BlockSize)
            throw new ArgumentException($"Sequence length {sequenceLength} exceeds BlockSize {_config.BlockSize}.");

        // Positions 0..sequenceLength-1, shared across the whole batch.
        var positions = arange(sequenceLength, dtype: ScalarType.Int64, device: tokenIds.device);

        var tokenEmbeddings = _tokenEmbedding.forward(tokenIds);       // (batch, seq, embedDim)
        var positionEmbeddings = _positionEmbedding.forward(positions); // (seq, embedDim), broadcasts over batch

        var x = _embeddingDropout.forward(tokenEmbeddings + positionEmbeddings);

        foreach (var block in _blocks)
        {
            x = block.forward(x);
        }

        x = _finalNorm.forward(x);
        return _lmHead.forward(x); // (batch, seq, vocabSize)
    }
}
