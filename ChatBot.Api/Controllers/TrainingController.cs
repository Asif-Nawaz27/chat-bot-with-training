using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using ChatBot.Api.Contracts;
using ChatBot.Api.Services;
using ChatBot.Data.Messaging;
using Microsoft.Extensions.Options;

namespace ChatBot.Api.Controllers;

/// <summary>
/// Trains a fresh mini-GPT model and saves the checkpoint to the shared Data folder.
/// Training runs as a background job (see <see cref="ITrainingJobService"/>) rather
/// than blocking the request, so a caller can poll for progress while it runs.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TrainingController : ControllerBase
{
    // Matches ASP.NET Core's default MVC JSON output (camelCase) - the SSE endpoint writes
    // JSON by hand instead of going through an output formatter, so it has to opt into the
    // same casing itself or the frontend's camelCase TrainingJobStatus parsing breaks.
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITrainingJobService _jobService;
    private readonly TrainingCallbackOptions _callbackOptions;

    public TrainingController(ITrainingJobService jobService, IOptions<TrainingCallbackOptions> callbackOptions)
    {
        _jobService = jobService;
        _callbackOptions = callbackOptions.Value;
    }

    /// <summary>POST api/training - starts training in the background and returns immediately with a job id.</summary>
    [HttpPost]
    public async Task<ActionResult<StartTrainingResponse>> Train([FromBody] TrainRequest? request)
    {
        try
        {
            var jobId = await _jobService.StartJobAsync(request);
            return Accepted(new StartTrainingResponse(jobId));
        }
        catch (FileNotFoundException ex)
        {
            return Problem(title: "Dataset not found.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// GET api/training/{jobId}/status?since=N - the job's status ("running", "completed"
    /// or "failed") plus any log lines produced since cursor <paramref name="since"/>.
    /// Pass the previous response's <c>nextCursor</c> back as <paramref name="since"/> to
    /// avoid re-fetching lines you've already seen.
    /// </summary>
    [HttpGet("{jobId}/status")]
    public ActionResult<TrainingJobStatusResponse> GetStatus(string jobId, [FromQuery] int since = 0)
    {
        try
        {
            return Ok(_jobService.GetStatus(jobId, since));
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Unknown training job.", detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// POST api/training/{jobId}/status-callback - called by ChatBot.Train (not the browser)
    /// to report a log line, completion, or failure. Requires the "X-Training-Api-Key" header
    /// to match <see cref="TrainingCallbackOptions.ApiKey"/>, configured identically on both
    /// sides - this replaced the status Service Bus queue, so it's the only thing standing
    /// between this endpoint and an arbitrary caller forging job status.
    /// </summary>
    [HttpPost("{jobId}/status-callback")]
    public IActionResult StatusCallback(string jobId, [FromBody] TrainingStatusMessage message)
    {
        var providedKey = Request.Headers["X-Training-Api-Key"].ToString();
        if (string.IsNullOrEmpty(_callbackOptions.ApiKey) || providedKey != _callbackOptions.ApiKey)
        {
            return Unauthorized();
        }

        if (message.JobId != jobId)
        {
            return BadRequest("Route jobId does not match the message body's JobId.");
        }

        _jobService.ApplyStatusUpdate(message);
        return Ok();
    }

    /// <summary>
    /// GET api/training/{jobId}/stream - Server-Sent Events stream of the job's status: an
    /// immediate snapshot of everything logged so far, then one push per subsequent update,
    /// until the job reaches "completed"/"failed" or the client disconnects. Replaces polling
    /// GET {jobId}/status on a timer with the browser's native EventSource.
    /// </summary>
    [HttpGet("{jobId}/stream")]
    public async Task Stream(string jobId, CancellationToken cancellationToken)
    {
        IAsyncEnumerable<TrainingJobStatusResponse> updates;
        try
        {
            updates = _jobService.SubscribeAsync(jobId, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(new { title = "Unknown training job.", detail = ex.Message }, cancellationToken);
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // Some reverse proxies (e.g. nginx) buffer responses by default, which would hold
        // every event until the stream ends - defeating the point of SSE.
        Response.Headers["X-Accel-Buffering"] = "no";
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        try
        {
            await foreach (var update in updates)
            {
                await Response.WriteAsync($"data: {JsonSerializer.Serialize(update, SseJsonOptions)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - nothing more to write.
        }
    }
}
