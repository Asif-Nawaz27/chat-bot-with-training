using ChatBot.Api.Contracts;
using ChatBot.Data.Messaging;

namespace ChatBot.Api.Services;

/// <summary>
/// Runs training as a background job instead of blocking an HTTP request for the
/// several minutes a full run can take, so the web UI can poll for progress (loss
/// per step, etc.) while it's running instead of staring at a spinner.
/// </summary>
public interface ITrainingJobService
{
    /// <summary>
    /// Starts training in the background and returns immediately with a job id. If
    /// <see cref="TrainRequest.DatasetBlobName"/> is given, its existence in blob storage is
    /// checked upfront so a typo'd or never-uploaded name fails the request immediately
    /// instead of surfacing only once the caller polls job status.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    /// <see cref="TrainRequest.DatasetBlobName"/> was given but no such dataset exists in blob storage.
    /// </exception>
    Task<string> StartJobAsync(TrainRequest? request);

    /// <summary>
    /// Returns the job's current status plus any log lines produced since <paramref name="since"/>
    /// (an opaque cursor - pass back the previous response's <c>NextCursor</c>).
    /// Throws <see cref="KeyNotFoundException"/> if the job id is unknown.
    /// </summary>
    TrainingJobStatusResponse GetStatus(string jobId, int since);

    /// <summary>
    /// Folds a progress/completion message received from ChatBot.Train (via the
    /// status-callback endpoint) into the job it refers to, and pushes it to any
    /// subscribers registered via <see cref="SubscribeAsync"/>. A no-op if the job id isn't
    /// known to this instance (e.g. the API restarted after the job started).
    /// </summary>
    void ApplyStatusUpdate(TrainingStatusMessage message);

    /// <summary>
    /// Streams the job's status as it changes - first the current state (so a subscriber
    /// that attaches mid-run still sees everything logged so far), then one item per
    /// subsequent <see cref="ApplyStatusUpdate"/> call, ending once the job reaches a
    /// terminal state ("completed"/"failed") or <paramref name="cancellationToken"/> fires
    /// (e.g. the client disconnected). Backs <c>GET api/training/{jobId}/stream</c>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The job id is unknown.</exception>
    IAsyncEnumerable<TrainingJobStatusResponse> SubscribeAsync(string jobId, CancellationToken cancellationToken);
}
