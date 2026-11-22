namespace DSDSplitter.Models;

/// <summary>一条日志记录。</summary>
public sealed record LogEntry(LogLevel Level, string Message);
