namespace DSDSplitter.Models;

/// <summary>
/// 日志级别，决定日志区文字颜色（见开发文档 4.2）。
/// </summary>
public enum LogLevel
{
    Info,    // 普通信息（灰色）
    Fix,     // [修复]（灰色）
    Warning, // [警告]（红色）
    Error,   // [错误]（红色）
    Skip,    // [跳过]（蓝色）
    Success, // 完成（绿色）
}
