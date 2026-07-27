namespace MiniGptChat;

/// <summary>
/// All the knobs that control model size and training behavior live here.
/// Keeping them in one place makes it easy to experiment (e.g. shrink the
/// model to train faster, or grow it once you have more data / patience).
/// </summary>
public class GptConfig
{
    // --- Model architecture ---

    /// <summary>Size of the token/position embedding vectors ("d_model" in the original paper).</summary>
    public int EmbedDim { get; set; } = 96;

    /// <summary>How many Transformer blocks (attention + feed-forward) are stacked.</summary>
    public int NumLayers { get; set; } = 3;

    /// <summary>How many attention heads each block uses. EmbedDim must be divisible by this.</summary>
    public int NumHeads { get; set; } = 4;

    /// <summary>
    /// Context window: the maximum number of previous tokens the model can look at
    /// when predicting the next one. Also called "block size" in nanoGPT-style code.
    /// </summary>
    public int BlockSize { get; set; } = 128;

    /// <summary>Dropout probability applied inside attention and the feed-forward layers.</summary>
    public double Dropout { get; set; } = 0.1;

    /// <summary>Hidden size of the feed-forward MLP inside each block, expressed as a multiple of EmbedDim.</summary>
    public int FeedForwardMultiplier { get; set; } = 4;

    /// <summary>
    /// Vocabulary size. This is NOT set by hand - it is filled in automatically
    /// once the tokenizer has scanned the training text and built its character vocabulary.
    /// </summary>
    public int VocabSize { get; set; }

    // --- Training ---

    /// <summary>How many optimizer steps to run.</summary>
    public int TrainingSteps { get; set; } = 3000;

    /// <summary>How many (input, target) sequences are processed per optimizer step.</summary>
    public int BatchSize { get; set; } = 32;

    /// <summary>Adam learning rate.</summary>
    public double LearningRate { get; set; } = 3e-4;

    /// <summary>How often (in steps) to print the current loss during training.</summary>
    public int LogEveryNSteps { get; set; } = 100;

    // --- File locations ---
    // Default to the shared Data folder at the solution root (see RepoPaths) so that
    // MiniGptChat.Cli and MiniGptChat.Api - which run from different working
    // directories - both read and write the exact same checkpoint files.

    /// <summary>Where the raw training text lives.</summary>
    public string TrainingDataPath { get; set; } = Path.Combine(RepoPaths.DataDirectory, "sample_conversations.txt");

    /// <summary>Where trained model weights are written/read.</summary>
    public string ModelWeightsPath { get; set; } = Path.Combine(RepoPaths.DataDirectory, "model.dat");

    /// <summary>Where the tokenizer's vocabulary mapping is written/read.</summary>
    public string VocabPath { get; set; } = Path.Combine(RepoPaths.DataDirectory, "vocab.json");

    /// <summary>Where this config's architecture settings are written/read (so chat mode rebuilds the exact same model shape).</summary>
    public string ConfigPath { get; set; } = Path.Combine(RepoPaths.DataDirectory, "model_config.json");
}
