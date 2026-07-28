using ChatBot.Api.Contracts;

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
}
