namespace MiniGptChat.Api.Services;

/// <summary>
/// Syncs the local Data/ folder with two Azure Blob Storage containers - "data" (training
/// text files) and "checkpoint" (trained model weights/vocab/config) - so a trained model
/// survives beyond a single machine's disk. When no connection string is configured,
/// every method becomes a no-op and the API behaves exactly as it does with pure local
/// file storage - Azure is an optional add-on, not a hard requirement to run this project.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>True if an AzureStorage connection string is configured.</summary>
    bool IsEnabled { get; }

    /// <summary>Creates the "data" and "checkpoint" containers if they don't already exist. No-op if disabled.</summary>
    Task EnsureContainersExistAsync();

    /// <summary>Uploads a local file to the "data" container under <paramref name="blobName"/>. No-op if disabled.</summary>
    Task UploadDatasetAsync(string blobName, string localFilePath);

    /// <summary>
    /// Uploads <paramref name="content"/> directly to the "data" container under <paramref name="blobName"/> -
    /// used by the dataset upload endpoint so an uploaded file goes straight to blob storage
    /// without ever being written to local disk. No-op if disabled.
    /// </summary>
    Task UploadDatasetStreamAsync(string blobName, Stream content);

    /// <summary>True if <paramref name="blobName"/> already exists in the "data" container. Always false if disabled.</summary>
    Task<bool> DatasetExistsAsync(string blobName);

    /// <summary>
    /// Opens a read stream directly against <paramref name="blobName"/> in the "data" container -
    /// used right before training starts so a blob-sourced dataset is streamed straight into the
    /// training pipeline without ever touching local disk. Returns null (without error) if
    /// disabled or the blob doesn't exist. Caller is responsible for disposing the stream.
    /// </summary>
    Task<Stream?> TryOpenDatasetReadStreamAsync(string blobName);

    /// <summary>Uploads a local file to the "checkpoint" container under <paramref name="blobName"/>. No-op if disabled.</summary>
    Task UploadCheckpointFileAsync(string blobName, string localFilePath);

    /// <summary>
    /// Downloads <paramref name="blobName"/> from the "checkpoint" container to <paramref name="localFilePath"/>.
    /// Returns false (without error) if disabled or the blob doesn't exist.
    /// </summary>
    Task<bool> TryDownloadCheckpointFileAsync(string blobName, string localFilePath);
}
