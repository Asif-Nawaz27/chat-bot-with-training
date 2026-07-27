namespace MiniGptChat.Corpus;

/// <summary>Loads the raw training text used for next-token prediction.</summary>
public interface ITrainingDataProvider
{
    /// <summary>Reads the full contents of the training text file at <paramref name="path"/>.</summary>
    string LoadText(string path);

    /// <summary>
    /// Reads the full contents of <paramref name="stream"/> as training text - used for
    /// sources that never touch local disk, e.g. a dataset streamed straight out of
    /// Azure Blob Storage.
    /// </summary>
    string LoadText(Stream stream);
}
