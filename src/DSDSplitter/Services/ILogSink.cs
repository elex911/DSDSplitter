using DSDSplitter.Models;

namespace DSDSplitter.Services;

/// <summary>
/// 日志接收端。UI 层实现此接口，把日志渲染到只读日志区。
/// Core 层只依赖该抽象，不引用 WinForms（保持可测试、可替换）。
/// </summary>
public interface ILogSink
{
    void Log(LogEntry entry);

    void Log(LogLevel level, string message) => Log(new LogEntry(level, message));
}
