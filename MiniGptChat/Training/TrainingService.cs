using MiniGptChat.Corpus;
using MiniGptChat.Model;
using MiniGptChat.Tokenization;
using static TorchSharp.torch;

namespace MiniGptChat.Training;

/// <summary>
/// Trains a <see cref="MiniGptModel"/> from scratch on a plain text file using
/// next-token prediction: for every position in the text, the model is asked
/// to predict the character that comes right after it. Repeating this many
/// times (across random chunks of the text) is the entire training signal -
/// there are no labels beyond "the next character".
/// </summary>
public class TrainingService : ITrainingService
{
    private readonly ITrainingDataProvider _dataProvider;
    private readonly ITokenizerService _tokenizerService;
    private readonly IModelService _modelService;
    private readonly IBatchSampler _batchSampler;

    public TrainingService(
        ITrainingDataProvider dataProvider,
        ITokenizerService tokenizerService,
        IModelService modelService,
        IBatchSampler batchSampler)
    {
        _dataProvider = dataProvider;
        _tokenizerService = tokenizerService;
        _modelService = modelService;
        _batchSampler = batchSampler;
    }

    public void Train(GptConfig config, Action<string>? onLog = null)
    {
        void Log(string message)
        {
            Console.WriteLine(message);
            onLog?.Invoke(message);
        }

        Log($"Loading training text from {config.TrainingDataPath} ...");
        var text = _dataProvider.LoadText(config.TrainingDataPath);
        Log($"Loaded {text.Length:N0} characters.");

        TrainCore(config, text, Log);
    }

    public void Train(GptConfig config, string trainingText, Action<string>? onLog = null)
    {
        void Log(string message)
        {
            Console.WriteLine(message);
            onLog?.Invoke(message);
        }

        Log($"Using {trainingText.Length:N0} streamed characters of training text.");
        TrainCore(config, trainingText, Log);
    }

    private void TrainCore(GptConfig config, string text, Action<string> log)
    {
        var tokenizer = BuildTokenizer(text, config, log);
        var data = EncodeCorpus(text, tokenizer, config);

        using var model = _modelService.CreateModel(config);
        model.train(); // enables dropout during training

        var optimizer = optim.Adam(model.parameters(), lr: config.LearningRate);

        log($"Training for {config.TrainingSteps} steps " +
            $"(batch size {config.BatchSize}, block size {config.BlockSize}, lr {config.LearningRate})...");
        RunTrainingLoop(model, optimizer, data, config, log);

        SaveCheckpoint(model, tokenizer, config, log);
    }

    /// <summary>Builds the character vocabulary from the training text and records its size in the config.</summary>
    private CharTokenizer BuildTokenizer(string text, GptConfig config, Action<string> log)
    {
        var tokenizer = _tokenizerService.BuildFromText(text);
        config.VocabSize = tokenizer.VocabSize;
        log($"Vocabulary size: {config.VocabSize} distinct characters.");
        return tokenizer;
    }

    /// <summary>Encodes the whole corpus once into a single long tensor of token ids.</summary>
    private static Tensor EncodeCorpus(string text, CharTokenizer tokenizer, GptConfig config)
    {
        if (text.Length <= config.BlockSize + 1)
        {
            throw new InvalidOperationException(
                $"Training text ({text.Length} chars) must be longer than BlockSize ({config.BlockSize}).");
        }

        return tensor(tokenizer.Encode(text), dtype: ScalarType.Int64);
    }

    private void RunTrainingLoop(MiniGptModel model, optim.Optimizer optimizer, Tensor data, GptConfig config, Action<string> log)
    {
        for (int step = 1; step <= config.TrainingSteps; step++)
        {
            var (inputs, targets) = _batchSampler.SampleBatch(data, config.BatchSize, config.BlockSize);

            var logits = model.forward(inputs); // (batch, seq, vocab)

            // Flatten batch and sequence dimensions so cross_entropy sees a plain
            // list of (prediction, correct-answer) pairs: one per character position.
            var flatLogits = logits.reshape(-1, config.VocabSize);
            var flatTargets = targets.reshape(-1);
            var loss = nn.functional.cross_entropy(flatLogits, flatTargets);

            optimizer.zero_grad();
            loss.backward();
            optimizer.step();

            if (step % config.LogEveryNSteps == 0 || step == 1 || step == config.TrainingSteps)
            {
                log($"step {step,6}/{config.TrainingSteps}  loss = {loss.item<float>():F4}");
            }
        }
    }

    private void SaveCheckpoint(MiniGptModel model, CharTokenizer tokenizer, GptConfig config, Action<string> log)
    {
        _modelService.SaveWeights(model, config.ModelWeightsPath);
        _tokenizerService.Save(tokenizer, config.VocabPath);

        // Architecture settings must be saved too, so chat mode can reconstruct a
        // MiniGptModel with the exact same shape before loading these weights into it
        // (TorchSharp's save/load only handles raw tensor values, not the architecture).
        _modelService.SaveConfig(config, config.ConfigPath);

        log($"Saved model weights to {config.ModelWeightsPath}");
        log($"Saved vocabulary to   {config.VocabPath}");
        log($"Saved model config to {config.ConfigPath}");
    }
}
