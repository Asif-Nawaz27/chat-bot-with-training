using System.Text;
using MiniGptChat.Model;
using MiniGptChat.Tokenization;
using static TorchSharp.torch;

namespace MiniGptChat.Generation;

/// <inheritdoc cref="ITextGenerationService"/>
public class TextGenerationService : ITextGenerationService
{
    public string Generate(MiniGptModel model, CharTokenizer tokenizer, GptConfig config, string prompt, GenerationOptions options)
    {
        model.eval(); // disables dropout so generation isn't affected by random regularization noise
        using var _ = no_grad(); // we're not training, so skip building a backprop graph (faster, less memory)

        var ids = tokenizer.Encode(prompt).ToList();
        var generated = new StringBuilder();

        for (int step = 0; step < options.MaxNewTokens; step++)
        {
            var nextId = PredictNextToken(model, tokenizer, config, ids, options);
            ids.Add(nextId);
            generated.Append(tokenizer.Decode(new[] { nextId }));

            var stopIndex = FindEndMarker(generated, options.EndMarker);
            if (stopIndex >= 0)
                return generated.ToString(0, stopIndex);
        }

        return generated.ToString();
    }

    /// <summary>Runs one forward pass and samples a single next token id from the model's output.</summary>
    private static long PredictNextToken(MiniGptModel model, CharTokenizer tokenizer, GptConfig config, List<long> ids, GenerationOptions options)
    {
        // The model can only look at up to BlockSize tokens at a time, so if the
        // conversation so far is longer than that, keep only the most recent slice.
        var contextIds = ids.Count > config.BlockSize
            ? ids.Skip(ids.Count - config.BlockSize).ToArray()
            : ids.ToArray();

        var input = tensor(contextIds, dtype: ScalarType.Int64).unsqueeze(0); // (1, seqLen)
        var logits = model.forward(input); // (1, seqLen, vocabSize)

        // We only care about the prediction for the very next character, which
        // comes from the last position in the sequence.
        var seqLen = input.shape[1];
        var nextTokenLogits = logits.select(1, seqLen - 1).squeeze(0); // (vocabSize,)

        return SampleFromLogits(nextTokenLogits, options);
    }

    /// <summary>
    /// Turns raw logits for one position into a single sampled token id, applying
    /// temperature scaling and top-k filtering along the way.
    /// </summary>
    private static long SampleFromLogits(Tensor logits, GenerationOptions options)
    {
        // Temperature scaling: dividing logits by a value < 1 sharpens the
        // distribution (more confident/predictable), dividing by a value > 1
        // flattens it (more random/creative).
        var scaledLogits = logits / Math.Max(options.Temperature, 1e-6);

        var vocabSize = (int)scaledLogits.shape[0];
        if (options.TopK > 0 && options.TopK < vocabSize)
        {
            // Keep only the K highest-scoring characters; everything else is
            // excluded by setting its score to -infinity so softmax gives it ~0
            // probability. This prevents the model from occasionally sampling a
            // very unlikely (often nonsensical) character.
            var (topValues, topIndices) = scaledLogits.topk(options.TopK);
            var filtered = full_like(scaledLogits, double.NegativeInfinity);
            filtered.scatter_(0, topIndices, topValues);
            scaledLogits = filtered;
        }

        var probabilities = nn.functional.softmax(scaledLogits, dim: 0);

        // Randomly draw one token id according to the probability distribution
        // (as opposed to always taking the argmax, which would make replies
        // deterministic and repetitive).
        var sampled = multinomial(probabilities, num_samples: 1);
        return sampled.item<long>();
    }

    /// <summary>Returns the index where <paramref name="endMarker"/> first appears in the generated text so far, or -1.</summary>
    private static int FindEndMarker(StringBuilder generated, string endMarker)
    {
        if (string.IsNullOrEmpty(endMarker))
            return -1;

        return generated.ToString().IndexOf(endMarker, StringComparison.Ordinal);
    }
}
