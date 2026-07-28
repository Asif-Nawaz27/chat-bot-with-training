namespace ChatBot.Api.Contracts;

/// <summary>
/// Optional overrides for the default training hyperparameters (see GptConfig).
/// <see cref="DataPath"/> and <see cref="DatasetBlobName"/> are mutually exclusive: pass
/// <see cref="DataPath"/> for an explicit local file path (CLI/curl style), or
/// <see cref="DatasetBlobName"/> to train on a file previously uploaded via
/// POST api/dataset while Azure Blob Storage is configured - its existence is checked
/// upfront (see TrainingJobService.StartJobAsync) and it's then streamed straight into
/// the training pipeline without ever touching local disk (see TrainingJobService.Run).
/// </summary>
public record TrainRequest(
    int? Steps,
    int? BatchSize,
    double? LearningRate,
    string? DataPath,
    int? LogEveryNSteps,
    string? DatasetBlobName);

public record TrainResponse(string Message, int Steps, int BatchSize, double LearningRate);

public record ModelStatusResponse(bool ModelTrained);

/// <summary>Returned immediately after POST api/training starts a background training job.</summary>
public record StartTrainingResponse(string JobId);

/// <summary>
/// Polled repeatedly while a job runs. <see cref="Logs"/> only contains lines produced
/// since the caller's last <c>since</c> cursor - pass <see cref="NextCursor"/> back as
/// <c>since</c> on the next poll to avoid re-fetching lines you already have.
/// </summary>
public record TrainingJobStatusResponse(
    string Status,
    IReadOnlyList<string> Logs,
    int NextCursor,
    string? ErrorMessage,
    TrainResponse? Result);

/// <summary>
/// Returned after a training text file is uploaded via POST api/dataset. When
/// <see cref="StoredInBlob"/> is true, <see cref="DatasetPath"/> is a blob name - pass it
/// back as <see cref="TrainRequest.DatasetBlobName"/>. Otherwise (Azure isn't configured)
/// it's a local file path - pass it back as <see cref="TrainRequest.DataPath"/> instead.
/// </summary>
public record UploadDatasetResponse(string DatasetPath, bool StoredInBlob, string FileName, int CharacterCount);
