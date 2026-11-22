namespace DSDSplitter.Models;

/// <summary>一张 CUE 表：可能含多个 FILE（多碟 / 拼盘）。</summary>
public sealed class CueSheet
{
    public string? Title { get; set; }

    public string? Performer { get; set; }

    public List<CueFileRef> Files { get; } = new();

    /// <summary>所有 FILE 下的全部音轨（扁平化，便于遍历）。</summary>
    public IEnumerable<CueTrack> AllTracks => Files.SelectMany(f => f.Tracks);
}
