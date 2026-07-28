namespace ChatBot.Api.Services;

/// <summary>
/// Bound from the "AppSetting" section of appsettings (see appsettings.json).
/// </summary>
public class AppSettingOptions
{
    /// <summary>
    /// Comma-separated list of allowed CORS origins, e.g. "http://localhost:5173,http://127.0.0.1:5173".
    /// </summary>
    public string Cors { get; set; } = string.Empty;
}
