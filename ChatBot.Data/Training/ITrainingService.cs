namespace ChatBot.Data.Training;

/// <summary>Trains a mini-GPT model from scratch and saves the resulting checkpoint.</summary>
public interface ITrainingService
{
    /// <summary>
    /// Runs training to completion, loading training text from <see cref="GptConfig.TrainingDataPath"/>.
    /// <paramref name="onLog"/>, if given, is invoked with every line that would otherwise only go to
    /// the console - the API uses this to capture progress for clients that poll for it (see
    /// ChatBot.Api's training job service), while the CLI can simply omit it and rely on the
    /// console output.
    /// </summary>
    void Train(GptConfig config, Action<string>? onLog = null);

    /// <summary>
    /// Runs training to completion against <paramref name="trainingText"/> directly, bypassing
    /// <see cref="GptConfig.TrainingDataPath"/> entirely - used when the dataset was streamed in
    /// from a source with no local file (e.g. Azure Blob Storage).
    /// </summary>
    void Train(GptConfig config, string trainingText, Action<string>? onLog = null);
}
