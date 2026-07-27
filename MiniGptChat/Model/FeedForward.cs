using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace MiniGptChat.Model;

/// <summary>
/// The "position-wise feed-forward" sub-layer used inside every Transformer
/// block. Attention lets tokens share information with each other; this MLP
/// then gives the model extra capacity to process each token's representation
/// individually. It expands the vector to a larger hidden size, applies a
/// non-linearity (GELU), then projects back down to the embedding size.
/// </summary>
public class FeedForward : Module<Tensor, Tensor>
{
    private readonly Linear _expand;
    private readonly Linear _project;
    private readonly GELU _activation;
    private readonly Dropout _dropout;

    public FeedForward(int embedDim, int hiddenDim, double dropout) : base(nameof(FeedForward))
    {
        _expand = Linear(embedDim, hiddenDim);
        _activation = GELU();
        _project = Linear(hiddenDim, embedDim);
        _dropout = Dropout(dropout);

        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        x = _expand.forward(x);
        x = _activation.forward(x);
        x = _project.forward(x);
        return _dropout.forward(x);
    }
}
