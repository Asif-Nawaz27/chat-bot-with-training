using System.Collections.Concurrent;
using ChatBot.Api.Contracts;
using ChatBot.Data.Chat;
using ChatBot.Data.Messaging;
using ChatBot.Data.Storage;
using Microsoft.Extensions.Options;

namespace ChatBot.Api.Services;

/// <inheritdoc cref="ITrainingJobService"/>
public class TrainingJobService : ITrainingJobService
{
    private readonly IChatSessionService _chatSessionService;
    private readonly IBlobStorageService _blobStorage;
    private readonly IServiceBusPublisher _publisher;
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public TrainingJobService(
        IChatSessionService chatSessionService,
        IBlobStorageService blobStorage,
        IServiceBusPublisher publisher,
        IOptions<ServiceBusOptions> serviceBusOptions)
    {
        _chatSessionService = chatSessionService;
        _blobStorage = blobStorage;
        _publisher = publisher;
        _serviceBusOptions = serviceBusOptions.Value;
    }

    public async Task<string> StartJobAsync(TrainRequest? request)
    {
        var datasetBlobName = request?.DatasetBlobName;

        // Checked upfront (before the job even exists) so a name that was never uploaded
        // fails the request right away, rather than only once the caller polls status.
        if (!string.IsNullOrEmpty(datasetBlobName) && !await _blobStorage.DatasetExistsAsync(datasetBlobName))
        {
            throw new FileNotFoundException(
                $"Dataset '{datasetBlobName}' was not found in Azure Blob Storage. Upload it first via POST api/dataset.");
        }

        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = new Job();

        // Hands the actual training off to ChatBot.Train via Service Bus instead of running
        // it in this process - progress/completion comes back on the status queue (see
        // TrainingStatusListener) and is folded into this same job via ApplyStatusUpdate.
        var message = new TrainingJobMessage(
            jobId,
            request?.Steps,
            request?.BatchSize,
            request?.LearningRate,
            request?.LogEveryNSteps,
            datasetBlobName);

        await _publisher.PublishAsync(_serviceBusOptions.TrainingQueueName, message);

        return jobId;
    }

    public TrainingJobStatusResponse GetStatus(string jobId, int since)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            throw new KeyNotFoundException($"Unknown training job '{jobId}'.");

        lock (job.Lock)
        {
            var newLogs = since < job.Logs.Count ? job.Logs.Skip(since).ToList() : new List<string>();
            return new TrainingJobStatusResponse(job.Status, newLogs, job.Logs.Count, job.ErrorMessage, job.Result);
        }
    }

    public void ApplyStatusUpdate(TrainingStatusMessage message)
    {
        if (!_jobs.TryGetValue(message.JobId, out var job))
            return;

        lock (job.Lock)
        {
            switch (message.Kind)
            {
                case TrainingStatusKind.Log:
                    job.Logs.Add(message.LogLine ?? string.Empty);
                    break;

                case TrainingStatusKind.Completed:
                    var result = message.Result!;
                    job.Result = new TrainResponse("Training complete.", result.Steps, result.BatchSize, result.LearningRate);
                    job.Status = "completed";
                    // ChatSessionService caches the loaded model for the lifetime of this
                    // process; without this, chat would keep answering with the pre-retrain
                    // weights. This process (the API) is the only one holding that cache, so
                    // invalidation has to happen here rather than in ChatBot.Train.
                    _chatSessionService.InvalidateModel();
                    break;

                case TrainingStatusKind.Failed:
                    job.ErrorMessage = message.ErrorMessage;
                    job.Status = "failed";
                    break;
            }
        }
    }

    private sealed class Job
    {
        public readonly List<string> Logs = new();
        public readonly object Lock = new();
        public string Status = "running";
        public string? ErrorMessage;
        public TrainResponse? Result;
    }
}
