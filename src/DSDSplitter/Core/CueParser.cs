using System.Text;
using DSDSplitter.Models;
using DSDSplitter.Services;

namespace DSDSplitter.Core;

/// <summary>
/// 把归一化后的 CUE 文本解析为 <see cref="CueSheet"/>，并完成结构纠错
/// （见开发文档 4.1 顺序 6/7/8/9/10；第 11 项时长填充由 Processor 在 ffprobe 后完成）。
/// </summary>
public static class CueParser
{
    private static readonly string[] AudioExtensions =
    {
        ".wav", ".flac", ".ape", ".diff", ".dsf", ".dff", ".wv", ".tta",
    };

    public static CueSheet Parse(string cueText, string cueDir, ILogSink log)
    {
        var sheet = new CueSheet();
        CueFileRef? currentFile = null;
        CueTrack? currentTrack = null;

        foreach (var raw in cueText.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var parts = Tokenize(line);
            if (parts.Length == 0) continue;

            var cmd = parts[0].ToUpperInvariant();
            switch (cmd)
            {
                case "TITLE":
                    if (currentTrack is not null) currentTrack.Title = TakeRest(parts, 1);
                    else if (currentFile is null) sheet.Title = TakeRest(parts, 1);
                    break;

                case "PERFORMER":
                    if (currentTrack is not null) currentTrack.Performer = TakeRest(parts, 1);
                    else if (currentFile is null) sheet.Performer = TakeRest(parts, 1);
                    break;

                case "FILE":
                {
                    // FILE "name" TYPE  →  name 可能含空格（已被引号合并成单个 token）
                    var type = parts.Length > 2 ? parts[^1] : string.Empty;
                    var name = parts.Length > 2
                        ? string.Join(' ', parts, 1, parts.Length - 2)
                        : TakeRest(parts, 1) ?? string.Empty;

                    currentFile = new CueFileRef
                    {
                        DeclaredName = name,
                        DeclaredType = type,
                        ResolvedAudioPath = ResolveAudio(name, cueDir, log),
                    };
                    currentTrack = null;
                    sheet.Files.Add(currentFile);
                    break;
                }

                case "TRACK":
                {
                    if (currentFile is null) break;
                    var number = int.TryParse(parts.Length > 1 ? parts[1] : null, out var n)
                        ? n
                        : currentFile.Tracks.Count + 1;
                    var isAudio = parts.Length <= 2
                                  || parts[2].Equals("AUDIO", StringComparison.OrdinalIgnoreCase);
                    currentTrack = new CueTrack { Number = number, IsAudio = isAudio };
                    currentFile.Tracks.Add(currentTrack);
                    break;
                }

                case "INDEX":
                {
                    if (currentTrack is null || parts.Length < 3) break;
                    var idxNo = int.TryParse(parts[1], out var i) ? i : -1;
                    var sec = CueTime.Parse(parts[2]);
                    if (idxNo == 0) currentTrack.Index00 = sec;
                    else if (idxNo == 1) currentTrack.Index01 = sec;
                    break;
                }

                // FLAGS / ISRC / PREGAP / POSTGAP / REM / CATALOG 暂不参与切分，忽略
            }
        }

        // 结构纠错（8/9/10）：每个 FILE 内的音频轨
        foreach (var file in sheet.Files)
            NormalizeTracks(file, log);

        return sheet;
    }

    private static void NormalizeTracks(CueFileRef file, ILogSink log)
    {
        var audio = file.Tracks.Where(t => t.IsAudio).ToList();
        if (audio.Count == 0)
        {
            file.Tracks.Clear();
            return;
        }

        // 10) 缺 INDEX 01 → 用上一轨起点填充（首轨用 0）
        for (var i = 0; i < audio.Count; i++)
        {
            if (audio[i].Index01.HasValue) continue;
            audio[i].Index01 = i > 0 ? audio[i - 1].Index01 : 0;
            log.Log(LogLevel.Warning,
                $"INDEX 01 缺失: TRACK {audio[i].Number:00} 已用上一轨起点填充");
        }

        // 8) 按 INDEX 01 升序重排
        var sorted = audio.OrderBy(t => t.Index01 ?? double.MaxValue).ToList();
        var reordered = sorted.Where((t, i) => !ReferenceEquals(t, audio[i])).Any();
        if (reordered)
            log.Log(LogLevel.Fix, "时间重排: 按 INDEX 01 升序重排 TRACK");

        // 9) 重新连续编号（仅音频轨）
        for (var i = 0; i < sorted.Count; i++)
        {
            if (sorted[i].Number != i + 1)
                log.Log(LogLevel.Fix, $"TRACK 重排: {sorted[i].Number:00} → {i + 1:00}");
            sorted[i].Number = i + 1;
        }

        file.Tracks.Clear();
        file.Tracks.AddRange(sorted);
    }

    /// <summary>
    /// FILE 文件名模糊匹配（开发文档 4.1 顺序 6/7）：
    /// 忽略大小写、忽略扩展名差异、允许同前缀；命中多个时优先选体积最大者。
    /// 不擅自改写 CUE 的 FILE 类型字段。
    /// </summary>
    private static string? ResolveAudio(string declaredName, string cueDir, ILogSink log)
    {
        if (string.IsNullOrWhiteSpace(declaredName)) return null;
        if (!Directory.Exists(cueDir)) return null;

        // 1) 声明路径直接存在（可能含子目录）
        var direct = Path.IsPathRooted(declaredName)
            ? declaredName
            : Path.Combine(cueDir, declaredName);
        if (File.Exists(direct)) return Path.GetFullPath(direct);

        var declaredBase = Path.GetFileNameWithoutExtension(declaredName);

        // 2) 同目录音频文件模糊匹配（按文件体积降序优先）
        try
        {
            var candidates = Directory.EnumerateFiles(cueDir)
                .Where(f => AudioExtensions.Contains(
                    Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(SafeFileLength)
                .ToList();

            var same = candidates.FirstOrDefault(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f), declaredBase,
                    StringComparison.OrdinalIgnoreCase));
            if (same is not null)
            {
                log.Log(LogLevel.Fix, $"FILE 匹配: \"{declaredName}\" → {Path.GetFileName(same)}");
                return Path.GetFullPath(same);
            }

            var prefix = candidates.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f)
                    .StartsWith(declaredBase, StringComparison.OrdinalIgnoreCase));
            if (prefix is not null)
            {
                log.Log(LogLevel.Fix, $"FILE 模糊匹配: \"{declaredName}\" → {Path.GetFileName(prefix)}");
                return Path.GetFullPath(prefix);
            }
        }
        catch
        {
            // 目录枚举失败（无权限等），交给上层报错
        }

        return null;
    }

    private static long SafeFileLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    /// <summary>引号感知的分词：把 "a b" 合并为一个 token 并去掉引号。</summary>
    private static string[] Tokenize(string line)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        var inQuote = false;
        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuote = !inQuote;
                continue;
            }
            if (!inQuote && (c == ' ' || c == '\t'))
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens.ToArray();
    }

    private static string? TakeRest(string[] parts, int start)
    {
        if (parts.Length <= start) return null;
        return string.Join(' ', parts, start, parts.Length - start).Trim();
    }
}
