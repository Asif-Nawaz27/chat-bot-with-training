using Microsoft.AspNetCore.Mvc;
using MiniGptChat.Api.Contracts;
using MiniGptChat.Api.Services;

namespace MiniGptChat.Api.Controllers;

/// <summary>Lets a caller upload a custom training text file to train on instead of the built-in sample dataset.</summary>
[ApiController]
[Route("api/[controller]")]
public class DatasetController : ControllerBase
{
    // Fallback for when Azure Blob Storage isn't configured: uploads always land at this
    // fixed path (overwriting any previous upload) rather than being named after whatever
    // the client sent, so there's exactly one "the custom dataset" to reason about, and no
    // path-from-client input ever reaches the filesystem.
    private static readonly string UploadedDatasetPath = Path.Combine(RepoPaths.DataDirectory, "uploaded_dataset.txt");

    private readonly IBlobStorageService _blobStorage;

    public DatasetController(IBlobStorageService blobStorage)
    {
        _blobStorage = blobStorage;
    }

    /// <summary>POST api/dataset - uploads a plain text file (multipart/form-data, field "file") to train on.</summary>
    [HttpPost]
    [RequestSizeLimit(20_000_000)] // 20 MB is plenty for a character-level training text file
    public async Task<ActionResult<UploadDatasetResponse>> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(title: "No file uploaded.", detail: "Attach a plain text file under the 'file' form field.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Buffered once so it can be both decoded (to check it's non-empty text and count
        // characters) and uploaded, without reading the underlying request stream twice.
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        buffer.Position = 0;

        string text;
        using (var reader = new StreamReader(buffer, leaveOpen: true))
        {
            text = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Problem(title: "Uploaded file is empty.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (_blobStorage.IsEnabled)
        {
            // Goes straight to blob storage under its own name - never written to local
            // disk here. A local copy only gets created later, lazily, as a temp cache
            // right before a training run actually needs to read it off disk.
            var blobName = Path.GetFileName(file.FileName); // defend against a path-like filename
            buffer.Position = 0;
            await _blobStorage.UploadDatasetStreamAsync(blobName, buffer);
            return Ok(new UploadDatasetResponse(blobName, StoredInBlob: true, file.FileName, text.Length));
        }

        // No Azure configured: fall back to local disk so the app still works fully offline.
        Directory.CreateDirectory(RepoPaths.DataDirectory);
        await System.IO.File.WriteAllTextAsync(UploadedDatasetPath, text);
        return Ok(new UploadDatasetResponse(UploadedDatasetPath, StoredInBlob: false, file.FileName, text.Length));
    }
}
