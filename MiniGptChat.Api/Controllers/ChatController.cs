using Microsoft.AspNetCore.Mvc;
using MiniGptChat.Api.Contracts;
using MiniGptChat.Api.Services;
using MiniGptChat.Chat;

namespace MiniGptChat.Api.Controllers;

/// <summary>
/// Session-based chat over HTTP: start a session, then post messages to it. Each
/// session keeps its own conversation history server-side (see ChatSessionService),
/// so callers only need to send the new message each time, not the whole transcript.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatSessionService _sessionService;
    private readonly IBlobStorageService _blobStorage;

    public ChatController(IChatSessionService sessionService, IBlobStorageService blobStorage)
    {
        _sessionService = sessionService;
        _blobStorage = blobStorage;
    }

    /// <summary>POST api/chat/sessions - starts a new conversation and returns its id.</summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<StartSessionResponse>> StartSession()
    {
        var config = new GptConfig();

        // The API only auto-syncs blob storage -> local disk once, at process startup
        // (see MiddlewareExtension.setServicesScop) - so a checkpoint trained (and
        // uploaded) from a different instance would otherwise never be picked up here.
        // Refresh from blob before checking/loading the local copy so a new session
        // always starts on the latest trained checkpoint.
        if (_blobStorage.IsEnabled)
        {
            var modelDownloaded = await _blobStorage.TryDownloadCheckpointFileAsync("model.dat", config.ModelWeightsPath);
            var vocabDownloaded = await _blobStorage.TryDownloadCheckpointFileAsync("vocab.json", config.VocabPath);
            var configDownloaded = await _blobStorage.TryDownloadCheckpointFileAsync("model_config.json", config.ConfigPath);

            if (modelDownloaded || vocabDownloaded || configDownloaded)
            {
                _sessionService.InvalidateModel();
            }
        }

        if (!_sessionService.ModelReady(config))
        {
            return Problem(
                title: "No trained model found.",
                detail: "Call POST api/training first to create a checkpoint.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var sessionId = _sessionService.StartSession();
        return Ok(new StartSessionResponse(sessionId));
    }

    /// <summary>POST api/chat/sessions/{sessionId}/messages - sends a message and returns the bot's reply.</summary>
    [HttpPost("sessions/{sessionId}/messages")]
    public ActionResult<ChatMessageResponse> SendMessage(string sessionId, [FromBody] ChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Problem(
                title: "Message is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!_sessionService.SessionExists(sessionId))
        {
            return Problem(
                title: "Unknown chat session.",
                detail: $"No session with id '{sessionId}'. Start one with POST api/chat/sessions.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var config = new GptConfig();
        var reply = _sessionService.SendMessage(config, sessionId, request.Message.Trim());
        return Ok(new ChatMessageResponse(sessionId, reply));
    }
}
