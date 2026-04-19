namespace CopilotSDKPlayground.RagApi.Services;

/// <summary>
/// Splits documents into overlapping chunks suitable for TF-IDF vectorisation.
/// Strategy: sentence-aware sliding window (~512 words, ~10% overlap).
/// </summary>
public static class DocumentChunker
{
    private const int TargetChunkWords = 512;
    private const int OverlapWords = 52; // ~10% of target

    public static IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Split on sentence boundaries (. ! ? followed by whitespace)
        var sentences = System.Text.RegularExpressions.Regex
            .Split(text.Trim(), @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        var chunks = new List<string>();
        var current = new List<string>();
        var wordCount = 0;
        var overlapBuffer = new Queue<string>();

        foreach (var sentence in sentences)
        {
            var sentenceWords = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            wordCount += sentenceWords;
            current.Add(sentence);

            if (wordCount >= TargetChunkWords)
            {
                var chunk = string.Join(" ", current);
                chunks.Add(chunk);

                // Keep last N words as overlap for the next chunk
                current.Clear();
                wordCount = 0;

                // Add trailing sentences to overlap buffer
                var overlapSentences = sentences
                    .Skip(sentences.Length - Math.Min(2, sentences.Length))
                    .ToArray();
                foreach (var s in overlapSentences)
                {
                    current.Add(s);
                    wordCount += s.Split(' ').Length;
                    if (wordCount >= OverlapWords) break;
                }
            }
        }

        // Flush remaining content
        if (current.Count > 0)
            chunks.Add(string.Join(" ", current));

        return chunks;
    }
}
