using System.Text;

namespace DSDSplitter.Core;

/// <summary>文件名清洗与路径防穿越（见开发文档 7）。</summary>
public static class PathSanitizer
{
    private static readonly char[] InvalidFileNameChars =
    {
        '\\', '/', ':', '*', '?', '"', '<', '>', '|',
    };

    /// <summary>把标题清洗成安全文件名片段：非法字符 → _，去掉首尾点，超长截断。</summary>
    public static string SanitizeFileName(string name, int maxLen = 180)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            sb.Append(Array.IndexOf(InvalidFileNameChars, ch) >= 0 ? '_' : ch);
        }

        var result = sb.ToString().Trim().Trim('.');
        if (result.Length > maxLen)
            result = result[..maxLen].TrimEnd();
        return result;
    }

    /// <summary>
    /// 防穿越：确保 candidate 的完整路径仍落在 baseDir 内。
    /// 用于校验输出文件名拼接后不会逃出 输出目录。
    /// </summary>
    public static bool IsWithin(string baseDir, string candidate)
    {
        try
        {
            var fullBase = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar);
            var fullCand = Path.GetFullPath(candidate);
            return fullCand.StartsWith(fullBase + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(fullCand, fullBase, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
