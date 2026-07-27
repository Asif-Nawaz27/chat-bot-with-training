namespace MiniGptChat.Model;

/// <summary>
/// Creates <see cref="MiniGptModel"/> instances and handles reading/writing the
/// three files a checkpoint is made of: the raw weights, the architecture
/// (<see cref="GptConfig"/>) needed to reconstruct a model of the same shape,
/// and (via <c>ITokenizerService</c>, called separately) the vocabulary.
/// </summary>
public interface IModelService
{
    /// <summary>Builds a fresh, untrained model with the given architecture.</summary>
    MiniGptModel CreateModel(GptConfig config);

    /// <summary>True if a full checkpoint (weights + vocab + config) exists on disk.</summary>
    bool CheckpointExists(GptConfig config);

    /// <summary>Writes the model's current weights to <paramref name="path"/>.</summary>
    void SaveWeights(MiniGptModel model, string path);

    /// <summary>Loads previously saved weights into <paramref name="model"/> in place.</summary>
    void LoadWeights(MiniGptModel model, string path);

    /// <summary>Saves the architecture settings so the exact same model shape can be rebuilt later.</summary>
    void SaveConfig(GptConfig config, string path);

    /// <summary>Loads previously saved architecture settings.</summary>
    GptConfig LoadConfig(string path);
}
