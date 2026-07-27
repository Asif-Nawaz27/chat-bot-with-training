namespace MiniGptChat.Generation;

/// <summary>Settings that control how text is sampled from the model.</summary>
public class GenerationOptions
{
    /// <summary>Hard cap on how many new characters to generate, even if no end marker is seen.</summary>
    public int MaxNewTokens { get; set; } = 200;

    /// <summary>
    /// Controls randomness. Values below 1.0 make the model more confident/predictable
    /// (closer to always picking the most likely next character); values above 1.0 make
    /// it more random/creative. 1.0 leaves the model's raw probabilities unchanged.
    /// </summary>
    public double Temperature { get; set; } = 0.8;

    /// <summary>
    /// If greater than 0, only the K most likely next characters are considered at each
    /// step (their probabilities are renormalized, everything else is excluded). This
    /// avoids occasionally sampling a wildly unlikely character. Set to 0 to disable.
    /// </summary>
    public int TopK { get; set; } = 20;

    /// <summary>
    /// Generation stops as soon as this string appears in the newly generated text
    /// (it is not included in the returned reply). In our "User: ...\nBot: ...\n"
    /// dataset format, a bot reply is always a single line, so a newline is a natural
    /// place to stop.
    /// </summary>
    public string EndMarker { get; set; } = "\n";
}
