using Microsoft.AspNetCore.Mvc;
using MiniGptChat.Api.Contracts;
using MiniGptChat.Model;

namespace MiniGptChat.Api.Controllers;

/// <summary>Reports whether a trained checkpoint is currently available.</summary>
[ApiController]
[Route("api/[controller]")]
public class ModelController : ControllerBase
{
    private readonly IModelService _modelService;

    public ModelController(IModelService modelService)
    {
        _modelService = modelService;
    }

    /// <summary>GET api/model/status - true once `POST api/training` has produced a checkpoint.</summary>
    [HttpGet("status")]
    public ActionResult<ModelStatusResponse> GetStatus()
    {
        var config = new GptConfig();
        return Ok(new ModelStatusResponse(_modelService.CheckpointExists(config)));
    }
}
