namespace DSDSplitter.Models;

/// <summary>输出文件已存在时的处理方式（见开发文档 5 / 7）。</summary>
public enum ExistingFileAction
{
    Skip,      // 跳过并记 [跳过]
    Overwrite, // 覆盖（ffmpeg -y）
}
