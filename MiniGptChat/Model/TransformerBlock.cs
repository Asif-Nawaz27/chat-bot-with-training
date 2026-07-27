using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace MiniGptChat.Model;

/// <summary>
/// One Transformer block: causal self-attention followed by a feed-forward MLP,
/// each wrapped in a residual connection and preceded by layer normalization
/// ("pre-norm", the arrangement used by GPT-2 and most modern Transformers -
/// it tends to train more stably than normalizing after the sub-layer).
///
/// Residual connections (the "x = x + subLayer(x)" pattern) let gradients flow
/// directly through the network during backpropagation, which is what makes it
/// possible to stack many of these blocks without training becoming unstable.
/// </summary>
public class TransformerBlock : Module<Tensor, Tensor>
{
    private readonly LayerNorm _norm1;
    private readonly CausalSelfAttention _attention;
    private readonly LayerNorm _norm2;
    private readonly FeedForward _feedForward;

    public TransformerBlock(GptConfig config) : base(nameof(TransformerBlock))
    {
        _norm1 = LayerNorm(config.EmbedDim);
        _attention = new CausalSelfAttention(config.EmbedDim, config.NumHeads, config.BlockSize, config.Dropout);

        _norm2 = LayerNorm(config.EmbedDim);
        _feedForward = new FeedForward(config.EmbedDim, config.EmbedDim * config.FeedForwardMultiplier, config.Dropout);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        // Pre-norm residual attention: normalize, attend, then add back to the input.
        x = x + _attention.forward(_norm1.forward(x));

        // Pre-norm residual feed-forward: normalize, transform, then add back.
        x = x + _feedForward.forward(_norm2.forward(x));

        return x;
    }
}
