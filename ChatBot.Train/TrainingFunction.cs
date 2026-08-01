using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ChatBot.Data;
using ChatBot.Data.Corpus;
using ChatBot.Data.Messaging;
using ChatBot.Data.Storage;
using ChatBot.Data.Training;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ChatBot.Train;

/// <summary>
/// Dequeues a <see cref="TrainingJobMessage"/> from the training queue, runs training via
/// the same ChatBot.Data services ChatBot.Api used to call in-process, and reports progress
/// back via <see cref="ITrainingStatusReporter"/> as <see cref="TrainingStatusMessage"/>s -
/// one per log line, then a final Completed or Failed message. See ChatBot.Api's
/// TrainingController.StatusCallback for the receiving side.
/// </summary>
public class TrainingFunction
{
    private readonly ITrainingService _trainingService;
    private readonly ITrainingDataProvider _dataProvider;
    private readonly IBlobStorageService _blobStorage;
    private readonly ITrainingStatusReporter _statusReporter;
    private readonly ILogger<TrainingFunction> _logger;

    public TrainingFunction(
        ITrainingService trainingService,
        ITrainingDataProvider dataProvider,
        IBlobStorageService blobStorage,
        ITrainingStatusReporter statusReporter,
        ILogger<TrainingFunction> logger)
    {
        _trainingService = trainingService;
        _dataProvider = dataProvider;
        _blobStorage = blobStorage;
        _statusReporter = statusReporter;
        _logger = logger;
    }

    [Function(nameof(TrainingFunction))]
    public async Task Run(
        [ServiceBusTrigger("%AzureServiceBus_TrainingQueueName%", Connection = "AzureServiceBus_ConnectionString")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        var job = JsonSerializer.Deserialize<TrainingJobMessage>(message.Body)
            ?? throw new InvalidOperationException("Training job message body could not be deserialized.");

        _logger.LogInformation("Starting training job {JobId}", job.JobId);
        _logger.LogInformation("Azure blob storage status {IsEnabled}", _blobStorage.IsEnabled);

        void Log(string line) =>
            _statusReporter.ReportAsync(new TrainingStatusMessage(job.JobId, TrainingStatusKind.Log, LogLine: line))
                .GetAwaiter().GetResult();

        try
        {
            var config = new GptConfig();
            if (job.Steps is int steps) config.TrainingSteps = steps;
            if (job.BatchSize is int batchSize) config.BatchSize = batchSize;
            if (job.LearningRate is double learningRate) config.LearningRate = learningRate;
            if (job.LogEveryNSteps is int logEveryNSteps) config.LogEveryNSteps = logEveryNSteps;

            if (!string.IsNullOrEmpty(job.DatasetBlobName))
            {
                Log($"Streaming dataset '{job.DatasetBlobName}' from Azure Blob Storage...");
                var blobStream = await _blobStorage.TryOpenDatasetReadStreamAsync(job.DatasetBlobName);
                if (blobStream is null)
                {
                    throw new FileNotFoundException($"Dataset '{job.DatasetBlobName}' was not found in Azure Blob Storage.");
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

            if (_blobStorage.IsEnabled)
            {
                Log("Uploading checkpoint to Azure Blob Storage...");
                await _blobStorage.UploadCheckpointFileAsync("model.dat", config.ModelWeightsPath);
                await _blobStorage.UploadCheckpointFileAsync("vocab.json", config.VocabPath);
                await _blobStorage.UploadCheckpointFileAsync("model_config.json", config.ConfigPath);
                Log("Checkpoint uploaded to Azure Blob Storage.");
            }

            await _statusReporter.ReportAsync(
                new TrainingStatusMessage(
                    job.JobId,
                    TrainingStatusKind.Completed,
                    Result: new TrainingResult(config.TrainingSteps, config.BatchSize, config.LearningRate)));

            await messageActions.CompleteMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Training job {JobId} failed", job.JobId);
            await _statusReporter.ReportAsync(
                new TrainingStatusMessage(job.JobId, TrainingStatusKind.Failed, ErrorMessage: ex.Message));

            // Let the trigger's normal retry/dead-letter behavior handle this delivery.
            throw;
        }
    }
}
