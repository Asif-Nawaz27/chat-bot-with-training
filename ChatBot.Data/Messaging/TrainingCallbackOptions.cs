namespace ChatBot.Data.Messaging;

/// <summary>
/// Bound from the "TrainingCallback" section of appsettings / local.settings.json.
/// ChatBot.Train uses <see cref="ApiBaseUrl"/> to call back into ChatBot.Api with training
/// progress instead of publishing to a status queue; both sides check <see cref="ApiKey"/>
/// so the callback endpoint can't be hit by an arbitrary caller.
/// </summary>
public class TrainingCallbackOptions
{
    /// <summary>ChatBot.Api's base URL, used only by ChatBot.Train to reach the callback endpoint.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Shared secret sent as the "X-Training-Api-Key" header and checked by ChatBot.Api.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
