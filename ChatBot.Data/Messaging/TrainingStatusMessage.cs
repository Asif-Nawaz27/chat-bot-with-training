namespace ChatBot.Data.Messaging;

/// <summary>Which kind of update a <see cref="TrainingStatusMessage"/> carries.</summary>
public enum TrainingStatusKind
{
    /// <summary>One more line for the job's log (see <see cref="TrainingStatusMessage.LogLine"/>).</summary>
    Log,

    /// <summary>Training finished successfully (see <see cref="TrainingStatusMessage.Result"/>).</summary>
    Completed,

    /// <summary>Training failed (see <see cref="TrainingStatusMessage.ErrorMessage"/>).</summary>
    Failed
}

/// <summary>
/// Sent by ChatBot.Train to ChatBot.Api's status-callback endpoint as training progresses -
/// one message per log line, plus a final Completed or Failed message. ChatBot.Api folds
/// these into the same in-memory job/log model and fans them out over SSE to subscribed
/// clients (see TrainingJobService.ApplyStatusUpdate / SubscribeAsync).
/// </summary>
public record TrainingStatusMessage(
    string JobId,
    TrainingStatusKind Kind,
    string? LogLine = null,
    string? ErrorMessage = null,
    TrainingResult? Result = null);

/// <summary>Training-run summary, deliberately shaped like ChatBot.Api's TrainResponse contract.</summary>
public record TrainingResult(int Steps, int BatchSize, double LearningRate);
