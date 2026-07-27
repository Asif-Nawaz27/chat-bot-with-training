using MiniGptChat.Generation;
using MiniGptChat.Model;
using MiniGptChat.Tokenization;

namespace MiniGptChat.Chat;

/// <inheritdoc cref="IChatModelLoader"/>
public class ChatModelLoader : IChatModelLoader
{
    private readonly IModelService _modelService;
    private readonly ITokenizerService _tokenizerService;

    public ChatModelLoader(IModelService modelService, ITokenizerService tokenizerService)
    {
        _modelService = modelService;
        _tokenizerService = tokenizerService;
    }

    public bool CheckpointExists(GptConfig config) => _modelService.CheckpointExists(config);

    public ChatModelContext Load(GptConfig config)
    {
        // The saved config describes the exact architecture (embed dim, layers, heads,
        // vocab size, block size) the weights were trained with - we must rebuild the
        // model with identical shapes before loading the weights in. Its file-path
        // fields reflect wherever training happened to run from, though, which can
        // differ from where we're loading now (e.g. MiniGptChat.Cli vs MiniGptChat.Api
        // have different working directories) - so those are always taken from the
        // caller's own (freshly resolved) config instead, never from the saved file.
        var modelConfig = _modelService.LoadConfig(config.ConfigPath);
        modelConfig.TrainingDataPath = config.TrainingDataPath;
        modelConfig.ModelWeightsPath = config.ModelWeightsPath;
        modelConfig.VocabPath = config.VocabPath;
        modelConfig.ConfigPath = config.ConfigPath;

        var tokenizer = _tokenizerService.Load(config.VocabPath);

        var model = _modelService.CreateModel(modelConfig);
        _modelService.LoadWeights(model, modelConfig.ModelWeightsPath);
        model.eval(); // inference mode: disables dropout

        var options = new GenerationOptions
        {
            MaxNewTokens = 200,
            Temperature = 0.8,
            TopK = 20,
            EndMarker = "\n",
        };

        return new ChatModelContext(model, tokenizer, modelConfig, options);
    }
}
