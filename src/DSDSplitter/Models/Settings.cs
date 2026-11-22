using System.Text.Json.Serialization;

namespace DSDSplitter.Models;

/// <summary>
/// 应用设置，持久化到 settings.json（见开发文档 5）。
/// 字段名用英文小驼峰，保证 JSON 稳定、跨版本可读。
/// </summary>
public sealed class Settings
{
    [JsonPropertyName("outputFormat")]
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Flac;

    /// <summary>true = 输出到 源目录\Split；false = 使用 CustomOutputDir。</summary>
    [JsonPropertyName("outputToSourceSplit")]
    public bool OutputToSourceSplit { get; set; } = true;

    [JsonPropertyName("customOutputDir")]
    public string? CustomOutputDir { get; set; }

    /// <summary>并发切轨数，1-8，默认 min(CPU核心数, 8)。</summary>
    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; } = Math.Min(Environment.ProcessorCount, 8);

    [JsonPropertyName("dsdTarget")]
    public DsdTarget DsdTarget { get; set; } = DsdTarget.Cd44k16;

    [JsonPropertyName("customSampleRate")]
    public int CustomSampleRate { get; set; } = 44100;

    [JsonPropertyName("customBitDepth")]
    public int CustomBitDepth { get; set; } = 16;

    [JsonPropertyName("playFinishSound")]
    public bool PlayFinishSound { get; set; } = true;

    [JsonPropertyName("keepOriginalCue")]
    public bool KeepOriginalCue { get; set; } = true;

    [JsonPropertyName("existingFileAction")]
    public ExistingFileAction ExistingFileAction { get; set; } = ExistingFileAction.Skip;

    /// <summary>并发数钳制到合法范围。</summary>
    public int SafeConcurrency => Math.Clamp(Concurrency, 1, 8);
}
