using static TorchSharp.torch;

namespace MiniGptChat.Training;

/// <inheritdoc cref="IBatchSampler"/>
public class RandomBatchSampler : IBatchSampler
{
    private readonly Random _random = new();

    public (Tensor Inputs, Tensor Targets) SampleBatch(Tensor data, int batchSize, int blockSize)
    {
        var dataLength = (int)data.shape[0];
        var inputRows = new Tensor[batchSize];
        var targetRows = new Tensor[batchSize];

        for (int i = 0; i < batchSize; i++)
        {
            int start = _random.Next(0, dataLength - blockSize - 1);
            inputRows[i] = data[start..(start + blockSize)];
            targetRows[i] = data[(start + 1)..(start + blockSize + 1)];
        }

        return (stack(inputRows), stack(targetRows));
    }
}
