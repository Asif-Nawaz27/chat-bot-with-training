namespace ChatBot.Data.Storage;

/// <summary>
/// Bound from the "AzureStorage" section of appsettings (see appsettings.Development.json,
/// which is where the real connection string belongs - never commit a live key to
/// appsettings.json). Leaving <see cref="ConnectionString"/> empty disables Azure sync
/// entirely; the API then behaves exactly as it does without Azure configured at all,
/// reading/writing only the local Data/ folder.
/// </summary>
public class BlobStorageOptions
{
    public bool IsEnabled { get; set; } = false;
    public string ConnectionString { get; set; } = string.Empty;
    public string DataContainerName { get; set; } = "data";
    public string CheckpointContainerName { get; set; } = "checkpoint";
}
