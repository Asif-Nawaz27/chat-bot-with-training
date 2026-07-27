using static TorchSharp.torch;

namespace MiniGptChat.Training;

/// <summary>Draws random training batches out of the encoded corpus.</summary>
public interface IBatchSampler
{
    /// <summary>
    /// Picks <paramref name="batchSize"/> random windows of length <paramref name="blockSize"/>
    /// out of <paramref name="data"/>. Returns (inputs, targets) where targets is the same
    /// window shifted one character to the right - i.e. at every position, the "label" is
    /// simply whatever character actually comes next in the real text.
    /// </summary>
    (Tensor Inputs, Tensor Targets) SampleBatch(Tensor data, int batchSize, int blockSize);
}
