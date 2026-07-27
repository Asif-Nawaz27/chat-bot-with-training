using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace MiniGptChat.Model;

/// <summary>
/// Multi-head causal self-attention.
///
/// "Self-attention" lets every position in the sequence look at every other
/// position and decide how much to "pay attention" to it when building its
/// own representation. "Causal" (a.k.a. masked) means a position is only
/// allowed to look at itself and earlier positions - never the future -
/// which is what makes autoregressive next-token generation possible.
/// "Multi-head" means we do this attention computation several times in
/// parallel with smaller vector sizes ("heads"), which lets the model track
/// several different kinds of relationships at once (e.g. one head might
/// learn grammar, another might learn topic).
/// </summary>
public class CausalSelfAttention : Module<Tensor, Tensor>
{
    private readonly int _numHeads;
    private readonly int _headDim;
    private readonly Linear _query;
    private readonly Linear _key;
    private readonly Linear _value;
    private readonly Linear _outputProjection;
    private readonly Dropout _attentionDropout;
    private readonly Dropout _residualDropout;
    private readonly Tensor _causalMask;

    public CausalSelfAttention(int embedDim, int numHeads, int blockSize, double dropout)
        : base(nameof(CausalSelfAttention))
    {
        if (embedDim % numHeads != 0)
            throw new ArgumentException($"EmbedDim ({embedDim}) must be divisible by NumHeads ({numHeads}).");

        _numHeads = numHeads;
        _headDim = embedDim / numHeads;

        // Separate linear layers that project the input into "query", "key" and
        // "value" vectors - the standard building blocks of attention. Query asks
        // "what am I looking for?", key answers "what do I contain?", and value
        // holds "what information do I pass along if I'm attended to?".
        _query = Linear(embedDim, embedDim);
        _key = Linear(embedDim, embedDim);
        _value = Linear(embedDim, embedDim);

        // After combining all heads back together, this projects the result
        // back into the residual stream.
        _outputProjection = Linear(embedDim, embedDim);

        _attentionDropout = Dropout(dropout);
        _residualDropout = Dropout(dropout);

        // Precompute a lower-triangular mask once: mask[i, j] == 1 means
        // position i is allowed to attend to position j. Since it's lower
        // triangular, position i can only see positions 0..i (itself and the past).
        _causalMask = tril(ones(blockSize, blockSize)).to_type(ScalarType.Bool);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        // x has shape (batch, sequenceLength, embedDim)
        var batch = x.shape[0];
        var sequenceLength = x.shape[1];
        var embedDim = x.shape[2];

        // Project the input into queries, keys and values, then reshape so that
        // the "heads" become their own dimension:
        // (batch, seq, embedDim) -> (batch, seq, numHeads, headDim) -> (batch, numHeads, seq, headDim)
        var q = _query.forward(x).view(batch, sequenceLength, _numHeads, _headDim).transpose(1, 2);
        var k = _key.forward(x).view(batch, sequenceLength, _numHeads, _headDim).transpose(1, 2);
        var v = _value.forward(x).view(batch, sequenceLength, _numHeads, _headDim).transpose(1, 2);

        // Scaled dot-product attention: how much should each position attend to
        // every other position? We scale by sqrt(headDim) to keep the values in a
        // range where softmax gradients don't vanish or explode.
        var attentionScores = matmul(q, k.transpose(-2, -1)) / Math.Sqrt(_headDim);

        // Apply the causal mask: positions that aren't allowed to be seen get
        // set to negative infinity, so after softmax their probability is ~0.
        var mask = _causalMask[..(int)sequenceLength, ..(int)sequenceLength];
        attentionScores = attentionScores.masked_fill(mask.logical_not(), double.NegativeInfinity);

        // Turn scores into probabilities that sum to 1 across the "keys" dimension.
        var attentionWeights = softmax(attentionScores, dim: -1);
        attentionWeights = _attentionDropout.forward(attentionWeights);

        // Weighted sum of value vectors according to the attention probabilities.
        var attentionOutput = matmul(attentionWeights, v);

        // Merge the heads back together: (batch, numHeads, seq, headDim) -> (batch, seq, embedDim)
        attentionOutput = attentionOutput.transpose(1, 2).contiguous().view(batch, sequenceLength, embedDim);

        var output = _outputProjection.forward(attentionOutput);
        return _residualDropout.forward(output);
    }
}
