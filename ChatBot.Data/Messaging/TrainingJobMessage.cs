namespace ChatBot.Data.Messaging;

/// <summary>
/// Sent by ChatBot.Api to the training queue when a training job starts; consumed by
/// ChatBot.Train, which applies the same optional hyperparameter overrides the API used
/// to apply in-process (see GptConfig) before running training.
/// </summary>
public record TrainingJobMessage(
    string JobId,
    int? Steps,
    int? BatchSize,
    double? LearningRate,
    int? LogEveryNSteps,
    string? DatasetBlobName);
