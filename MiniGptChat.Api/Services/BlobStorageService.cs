using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace MiniGptChat.Api.Services;

/// <inheritdoc cref="IBlobStorageService"/>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient? _dataContainer;
    private readonly BlobContainerClient? _checkpointContainer;
    private readonly ILogger<BlobStorageService> _logger;

    public bool IsEnabled { get; }

    public BlobStorageService(IOptions<BlobStorageOptions> options, ILogger<BlobStorageService> logger)
    {
        _logger = logger;
        var config = options.Value;

        var serviceClient = new BlobServiceClient(config.ConnectionString);
        _dataContainer = serviceClient.GetBlobContainerClient(config.DataContainerName);
        _checkpointContainer = serviceClient.GetBlobContainerClient(config.CheckpointContainerName);
        IsEnabled = config.IsEnabled;
    }

    public async Task EnsureContainersExistAsync()
    {
        if (!IsEnabled) return;

        await _dataContainer!.CreateIfNotExistsAsync();
        await _checkpointContainer!.CreateIfNotExistsAsync();
        _logger.LogInformation("Azure Blob Storage containers ready: {DataContainer}, {CheckpointContainer}",
            _dataContainer.Name, _checkpointContainer.Name);
    }

    public Task UploadDatasetAsync(string blobName, string localFilePath) =>
        UploadFileAsync(_dataContainer, blobName, localFilePath);

    public Task UploadCheckpointFileAsync(string blobName, string localFilePath) =>
        UploadFileAsync(_checkpointContainer, blobName, localFilePath);

    public Task UploadDatasetStreamAsync(string blobName, Stream content) =>
        UploadStreamAsync(_dataContainer, blobName, content);

    private async Task UploadFileAsync(BlobContainerClient? container, string blobName, string localFilePath)
    {
        if (!IsEnabled) return;

        await using var stream = File.OpenRead(localFilePath);
        await UploadStreamAsync(container, blobName, stream);
    }

    private async Task UploadStreamAsync(BlobContainerClient? container, string blobName, Stream content)
    {
        if (!IsEnabled) return;

        await container!.GetBlobClient(blobName).UploadAsync(content, overwrite: true);
        _logger.LogInformation("Uploaded to {Container}/{BlobName}", container.Name, blobName);
    }

    public async Task<bool> DatasetExistsAsync(string blobName)
    {
        if (!IsEnabled) return false;
        return await _dataContainer!.GetBlobClient(blobName).ExistsAsync();
    }

    public async Task<Stream?> TryOpenDatasetReadStreamAsync(string blobName)
    {
        if (!IsEnabled) return null;

        var blobClient = _dataContainer!.GetBlobClient(blobName);
        if (!await blobClient.ExistsAsync())
            return null;

        _logger.LogInformation("Streaming {Container}/{BlobName}", _dataContainer.Name, blobName);
        return await blobClient.OpenReadAsync();
    }

    public Task<bool> TryDownloadCheckpointFileAsync(string blobName, string localFilePath) =>
        TryDownloadAsync(_checkpointContainer, blobName, localFilePath);

    private async Task<bool> TryDownloadAsync(BlobContainerClient? container, string blobName, string localFilePath)
    {
        if (!IsEnabled) return false;

        var blobClient = container!.GetBlobClient(blobName);
        if (!await blobClient.ExistsAsync())
            return false;

        var directory = Path.GetDirectoryName(localFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await blobClient.DownloadToAsync(localFilePath);
        _logger.LogInformation("Downloaded {Container}/{BlobName} to {LocalPath}", container.Name, blobName, localFilePath);
        return true;
    }
}
