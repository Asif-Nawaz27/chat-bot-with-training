using System.Text.Json;

namespace MiniGptChat.Model;

/// <inheritdoc cref="IModelService"/>
public class ModelService : IModelService
{
    public MiniGptModel CreateModel(GptConfig config) => new(config);

    public bool CheckpointExists(GptConfig config) =>
        File.Exists(config.ModelWeightsPath) && File.Exists(config.VocabPath) && File.Exists(config.ConfigPath);

    public void SaveWeights(MiniGptModel model, string path)
    {
        EnsureDirectoryExists(path);
        model.save(path);
    }

    public void LoadWeights(MiniGptModel model, string path) => model.load(path);

    public void SaveConfig(GptConfig config, string path)
    {
        EnsureDirectoryExists(path);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public GptConfig LoadConfig(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<GptConfig>(json)
            ?? throw new InvalidDataException($"Could not parse model config at {path}");
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }
}
