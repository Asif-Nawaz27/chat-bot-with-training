using System.Collections.Concurrent;
using MiniGptChat.Api.Contracts;
using MiniGptChat.Chat;
using MiniGptChat.Corpus;
using MiniGptChat.Training;

namespace MiniGptChat.Api.Services;

/// <inheritdoc cref="ITrainingJobService"/>
public class TrainingJobService : ITrainingJobService
{
    private readonly ITrainingService _trainingService;
    private readonly IChatSessionService _chatSessionService;
    private readonly IBlobStorageService _blobStorage;
    private readonly ITrainingDataProvider _dataProvider;
    private readonly ConcurrentDictionary<string, Job> _jobs = new();

    public TrainingJobService(
        ITrainingService trainingService,
        IChatSessionService chatSessionService,
        IBlobStorageService blobStorage,
        ITrainingDataProvider dataProvider)
    {
        _trainingService = trainingService;
        _chatSessionService = chatSessionService;
        _blobStorage = blobStorage;
        _dataProvider = dataProvider;
    }

    public async Task<string> StartJobAsync(TrainRequest? request)
    {
        var config = new GptConfig();
        if (request?.Steps is int steps) config.TrainingSteps = steps;
        if (request?.BatchSize is int batchSize) config.BatchSize = batchSize;
        if (request?.LearningRate is double learningRate) config.LearningRate = learningRate;
        if (request?.DataPath is string dataPath) config.TrainingDataPath = dataPath;
        if (request?.LogEveryNSteps is int logEveryNSteps) config.LogEveryNSteps = logEveryNSteps;

        var datasetBlobName = request?.DatasetBlobName;

        // Checked upfront (before the job even exists) so a name that was never uploaded
        // fails the request right away, rather than only once the caller polls status.
        if (!string.IsNullOrEmpty(datasetBlobName) && !await _blobStorage.DatasetExistsAsync(datasetBlobName))
        {
            throw new FileNotFoundException(
                $"Dataset '{datasetBlobName}' was not found in Azure Blob Storage. Upload it first via POST api/dataset.");
        }

        var job = new Job();
        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = job;

        // Fire-and-forget: the job object is how the caller finds out what happened,
        // since the HTTP request that started it returns immediately.
        _ = Task.Run(() => Run(job, config, datasetBlobName));

        return jobId;
    }

    private async Task Run(Job job, GptConfig config, string? datasetBlobName)
    {
        void Log(string message)
        {
            lock (job.Lock)
            {
                job.Logs.Add(message);
            }
        }

        try
        {
            // A dataset uploaded straight to blob storage (see DatasetController) never
            // touched local disk - stream it straight into the training pipeline instead
            // of downloading it to a temp file first, so it never touches disk here either.
            if (!string.IsNullOrEmpty(datasetBlobName))
            {
                Log($"Streaming dataset '{datasetBlobName}' from Azure Blob Storage...");
                var blobStream = await _blobStorage.TryOpenDatasetReadStreamAsync(datasetBlobName);
                if (blobStream is null)
                {
                    throw new FileNotFoundException($"Dataset '{datasetBlobName}' was not found in Azure Blob Storage.");
                }

                string trainingText;
                await using (blobStream)
                {
                    trainingText = _dataProvider.LoadText(blobStream);
                }
                Log($"Streamed {trainingText.Length:N0} characters.");

                _trainingService.Train(config, trainingText, Log);
            }
            else
            {
                _trainingService.Train(config, Log);
            }

            // ChatSessionService caches the loaded model for the lifetime of the process;
            // without this, chat would keep answering with the pre-retrain weights.
            _chatSessionService.InvalidateModel();

            // Mirrors the freshly saved checkpoint into the "checkpoint" container when
            // Azure Storage is configured (a no-op otherwise), so it survives beyond
            // this machine's disk and other instances can pick it up on startup.
            if (_blobStorage.IsEnabled)
            {
                Log("Uploading checkpoint to Azure Blob Storage...");
                await _blobStorage.UploadCheckpointFileAsync("model.dat", config.ModelWeightsPath);
                await _blobStorage.UploadCheckpointFileAsync("vocab.json", config.VocabPath);
                await _blobStorage.UploadCheckpointFileAsync("model_config.json", config.ConfigPath);
                Log("Checkpoint uploaded to Azure Blob Storage.");
            }

            lock (job.Lock)
            {
                job.Result = new TrainResponse("Training complete.", config.TrainingSteps, config.BatchSize, config.LearningRate);
                job.Status = "completed";
            }
        }
        catch (Exception ex)
        {
            lock (job.Lock)
            {
                job.ErrorMessage = ex.Message;
                job.Status = "failed";
            }
        }
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

    private sealed class Job
    {
        public readonly List<string> Logs = new();
        public readonly object Lock = new();
        public string Status = "running";
        public string? ErrorMessage;
        public TrainResponse? Result;
    }
}
