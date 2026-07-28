using Microsoft.AspNetCore.Mvc;
using ChatBot.Api.Contracts;
using ChatBot.Api.Services;

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
    private readonly ITrainingJobService _jobService;

    public TrainingController(ITrainingJobService jobService)
    {
        _jobService = jobService;
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
}
