using System.Net.Http.Json;
using ChatBot.Data.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChatBot.Train;

/// <summary>Reports training progress/completion to ChatBot.Api. See <see cref="TrainingFunction"/>.</summary>
public interface ITrainingStatusReporter
{
    Task ReportAsync(TrainingStatusMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Calls ChatBot.Api's POST api/training/{jobId}/status-callback directly instead of
/// publishing to a status queue - see TrainingCallbackOptions for the shared secret both
/// sides check. A dropped log line after retries is tolerated (best-effort console output),
/// but a Completed/Failed message that still can't be delivered is rethrown so
/// <see cref="TrainingFunction.Run"/>'s catch block fires, letting the training queue's own
/// retry/dead-letter handle the job.
/// </summary>
public class TrainingStatusReporter : ITrainingStatusReporter
{
    private const int MaxAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly TrainingCallbackOptions _options;
    private readonly ILogger<TrainingStatusReporter> _logger;

    public TrainingStatusReporter(HttpClient httpClient, IOptions<TrainingCallbackOptions> options, ILogger<TrainingStatusReporter> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(_options.ApiBaseUrl);
        _logger = logger;
    }

    public async Task ReportAsync(TrainingStatusMessage message, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var request = BuildRequest(message);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Status callback attempt {Attempt}/{MaxAttempts} failed for job {JobId} ({Kind})",
                    attempt, MaxAttempts, message.JobId, message.Kind);
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
            catch when (message.Kind == TrainingStatusKind.Log)
            {
                _logger.LogWarning(
                    "Dropping a training log line after {MaxAttempts} failed callback attempts for job {JobId}.",
                    MaxAttempts, message.JobId);
                return;
            }
            // Completed/Failed on the final attempt falls through here uncaught - the
            // exception propagates to the caller by design (see class remarks).
        }
    }

    private HttpRequestMessage BuildRequest(TrainingStatusMessage message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"api/training/{message.JobId}/status-callback")
        {
            Content = JsonContent.Create(message)
        };
        request.Headers.Add("X-Training-Api-Key", _options.ApiKey);
        return request;
    }
}
