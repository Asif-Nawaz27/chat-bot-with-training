using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ChatBot.Data.Messaging;
using Microsoft.Extensions.Options;

namespace ChatBot.Api.Services;

/// <summary>
/// Runs for the lifetime of the API process, listening on <see cref="ServiceBusOptions.StatusQueueName"/>
/// for the progress/completion messages ChatBot.Train sends while it trains, and folding each
/// one into the matching job via <see cref="ITrainingJobService.ApplyStatusUpdate"/>. This is
/// what lets <c>GET /api/training/{jobId}/status</c> keep working exactly as it did when
/// training ran in-process.
/// </summary>
public class TrainingStatusListener : BackgroundService
{
    private readonly ITrainingJobService _jobService;
    private readonly ILogger<TrainingStatusListener> _logger;
    private readonly ServiceBusClient _client;
    private readonly string _statusQueueName;
    private ServiceBusProcessor? _processor;

    public TrainingStatusListener(
        ITrainingJobService jobService,
        IOptions<ServiceBusOptions> options,
        ILogger<TrainingStatusListener> logger)
    {
        _jobService = jobService;
        _logger = logger;
        _statusQueueName = options.Value.StatusQueueName;
        _client = new ServiceBusClient(options.Value.ConnectionString);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor(_statusQueueName);
        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        // Keep the background service alive until the host shuts down; the processor itself
        // dispatches messages to OnMessageAsync on its own background threads in the meantime.
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown - StopAsync tears down the processor below.
        }
    }

    private Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = JsonSerializer.Deserialize<TrainingStatusMessage>(args.Message.Body);
        if (message is not null)
        {
            _jobService.ApplyStatusUpdate(message);
        }

        return args.CompleteMessageAsync(args.Message);
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error receiving from Service Bus queue {Queue}", _statusQueueName);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }
        await _client.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
