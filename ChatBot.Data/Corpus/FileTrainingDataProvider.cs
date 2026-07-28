namespace ChatBot.Data.Corpus;

/// <inheritdoc cref="ITrainingDataProvider"/>
public class FileTrainingDataProvider : ITrainingDataProvider
{
    public string LoadText(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Training data not found at '{path}'. Add a text file there, " +
                "or point GptConfig.TrainingDataPath at your own data.");
        }

        using var stream = File.OpenRead(path);
        return LoadText(stream);
    }

    public string LoadText(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
