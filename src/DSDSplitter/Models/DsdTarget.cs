namespace DSDSplitter.Models;

/// <summary>DSD 转 PCM 的目标规格（见开发文档 5）。</summary>
public enum DsdTarget
{
    Cd44k16, // 44.1kHz / 16bit（CD 标准）
    Hq88k24, // 88.2kHz / 24bit
    Custom,  // 自定义（使用 Settings.CustomSampleRate / CustomBitDepth）
}
