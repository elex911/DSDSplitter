namespace DSDSplitter.Core;

/// <summary>待处理任务的类型。</summary>
public enum SplitKind
{
    /// <summary>有 CUE → 按音轨分轨。</summary>
    CueSplit,

    /// <summary>只有音频、找不到 CUE → 整轨转换。</summary>
    WholeConvert,
}

/// <summary>
/// 一个待处理任务（由 FileClassifier 产生，Processor 消费）。
/// </summary>
/// <param name="Kind">任务类型</param>
/// <param name="SourcePath">触发文件（cue 或音频）的绝对路径</param>
/// <param name="CuePath">CUE 绝对路径；整轨转换时为 null</param>
/// <param name="AudioPath">音频绝对路径；CUE 未找到音频时为 null</param>
/// <param name="BaseDir">源目录（输出 源目录\Split\ 的基准）</param>
public sealed record SplitJob(
    SplitKind Kind,
    string SourcePath,
    string? CuePath,
    string? AudioPath,
    string BaseDir);
