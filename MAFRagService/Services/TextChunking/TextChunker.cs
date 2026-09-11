namespace MAFRagService.Services.TextChunking;

/// <summary>
/// 按字符 + 标点边界分块。
/// 优先在句号 / 换行处切分；保证 overlap 单调前进，避免死循环。
/// </summary>
public sealed class TextChunker : ITextChunker
{
    private static readonly char[] BoundaryChars =
        { '\n', '。', '.', '!', '！', '?', '？', ';', '；' };

    public IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<TextChunk>();
        if (options.MaxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaxChars));
        if (options.OverlapChars < 0 || options.OverlapChars >= options.MaxChars)
            throw new ArgumentOutOfRangeException(nameof(options.OverlapChars),
                "OverlapChars 必须 >= 0 且 < MaxChars。");

        var result = new List<TextChunk>();
        var step   = options.MaxChars - options.OverlapChars;
        var start  = 0;
        var index  = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + options.MaxChars, text.Length);
            if (end < text.Length) end = SnapToBoundary(text, start, end);

            var slice = text.Substring(start, end - start).Trim();
            if (slice.Length >= options.MinChars || end == text.Length)
                result.Add(new TextChunk(index++, slice, start, end));

            if (end >= text.Length) break;

            // 单调前进
            start = Math.Max(end - options.OverlapChars, start + step);
        }

        return result;
    }

    private static int SnapToBoundary(string text, int start, int end)
    {
        var floor = start + (end - start) / 2;
        for (var j = end; j > floor; j--)
        {
            if (Array.IndexOf(BoundaryChars, text[j - 1]) >= 0) return j;
        }
        return end;
    }
}