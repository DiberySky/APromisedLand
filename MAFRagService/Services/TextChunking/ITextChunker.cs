namespace MAFRagService.Services.TextChunking;

public sealed record TextChunk(int Index, string Text, int StartOffset, int EndOffset);

public sealed class ChunkOptions
{
    /// <summary>单块最大字符数。</summary>
    public int MaxChars { get; init; } = 800;

    /// <summary>相邻块的重叠字符数，必须小于 MaxChars。</summary>
    public int OverlapChars { get; init; } = 100;

    /// <summary>小于该长度的碎片会被丢弃（最后一块除外）。</summary>
    public int MinChars { get; init; } = 20;
}

public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text, ChunkOptions options);
}