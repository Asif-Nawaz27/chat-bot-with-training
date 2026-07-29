using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
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

        TrainingJobStatusResponse update;
        List<Channel<TrainingJobStatusResponse>> subscribers;
        var terminal = message.Kind is TrainingStatusKind.Completed or TrainingStatusKind.Failed;

        lock (job.Lock)
        {
            switch (message.Kind)
            {
                case TrainingStatusKind.Log:
                    var line = message.LogLine ?? string.Empty;
                    job.Logs.Add(line);
                    update = new TrainingJobStatusResponse(job.Status, new[] { line }, job.Logs.Count, null, null);
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
                    update = new TrainingJobStatusResponse(job.Status, Array.Empty<string>(), job.Logs.Count, null, job.Result);
                    break;

                case TrainingStatusKind.Failed:
                    job.ErrorMessage = message.ErrorMessage;
                    job.Status = "failed";
                    update = new TrainingJobStatusResponse(job.Status, Array.Empty<string>(), job.Logs.Count, job.ErrorMessage, null);
                    break;

                default:
                    return;
            }

            subscribers = job.Subscribers.ToList();
            if (terminal) job.Subscribers.Clear();
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.Writer.TryWrite(update);
            if (terminal) subscriber.Writer.TryComplete();
        }
    }

    public IAsyncEnumerable<TrainingJobStatusResponse> SubscribeAsync(string jobId, CancellationToken cancellationToken)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            throw new KeyNotFoundException($"Unknown training job '{jobId}'.");

        return StreamAsync(job, cancellationToken);
    }

    // A plain (non-iterator) method wraps this so the KeyNotFoundException above is thrown
    // immediately when SubscribeAsync is called, not deferred until the first MoveNextAsync -
    // C# defers everything in an iterator method's body until enumeration actually starts.
    private static async IAsyncEnumerable<TrainingJobStatusResponse> StreamAsync(
        Job job, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<TrainingJobStatusResponse>();

        TrainingJobStatusResponse snapshot;
        lock (job.Lock)
        {
            snapshot = new TrainingJobStatusResponse(job.Status, job.Logs.ToList(), job.Logs.Count, job.ErrorMessage, job.Result);
            if (job.Status == "running")
                job.Subscribers.Add(channel);
            else
                channel.Writer.TryComplete();
        }

        yield return snapshot;

        try
        {
            await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            lock (job.Lock)
            {
                job.Subscribers.Remove(channel);
            }
        }
    }

    private sealed class Job
    {
        public readonly List<string> Logs = new();
        public readonly object Lock = new();
        public readonly List<Channel<TrainingJobStatusResponse>> Subscribers = new();
        public string Status = "running";
        public string? ErrorMessage;
        public TrainResponse? Result;
    }
}
