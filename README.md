# DSD Splitter

单文件绿色 CUE 分轨工具 —— **拖进去就切**。  
C# / .NET 8 / WinForms，基于 ffmpeg，带 CUE 静默纠错与 DSD→PCM 转码。

## 功能

- **CUE 分轨**：拖入 `.cue`（或音频/文件夹）自动按 INDEX 01 切成分轨文件，写回标题/艺术家/专辑/轨号元数据。
- **DSD 转 PCM**：`.dsf` / `.dff` / `.diff` 自动转 FLAC / WAV / MP3，支持 CD 标准（44.1kHz/16bit）、88.2kHz/24bit 或自定义采样率/位深（16/24bit）。
- **CUE 静默纠错**：编码探测（UTF-8 / GB18030 / Big5 / Shift-JIS）、去 BOM、统一换行、引号补全（含不闭合引号与未加引号的 FILE 空格文件名）、关键字大写、控制字符清洗、FILE 模糊匹配（多候选按体积优先）、时间重排、缺 INDEX 01 填充、末轨时长补全 —— 全程只记日志不弹窗。
- **单文件绿色**：Release 单文件发布 + 压缩，免安装双击即跑；设置便携存 exe 同目录，不可写时回退 `%APPDATA%\DSDSplitter`。
- 队列、并发、进度条与剩余时间（ETA）、Esc 取消、可配置的完成提示音与托盘通知。

## 如何生成 Windows 可执行文件（`DSDSplitter.exe`）

### 方式一：本地 Windows 一键编译（推荐）
1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
2. （可选）把 Windows 版 `ffmpeg.exe` 与 `ffprobe.exe` 放到 `tools/` 目录。
3. 双击运行根目录下的 **`build.bat`**，生成产物位于 `dist\DSDSplitter\`。

### 方式二：零安装云端自动编译（GitHub Actions）
如果本机不想安装 .NET 8 SDK：
1. 将本目录推送到任意 GitHub 仓库（支持私有仓库）。
2. 打开仓库的 **Actions** 页面，运行 **Build Windows Release** 工作流。
3. 约 2 分钟后在 Artifacts 中直接下载 **`DSDSplitter-win-x64.zip`**（已自动打包 `DSDSplitter.exe` + `ffmpeg.exe` + `ffprobe.exe`，解压即用）。

## 使用

1. 确保 `ffmpeg.exe` 和 `ffprobe.exe` 与 `DSDSplitter.exe` 在同一目录（或已加入系统 PATH）。
2. 双击运行 `DSDSplitter.exe`，把 `.cue`、音频文件或文件夹拖进窗口上方区域即可开始。
3. 右下角「⚙ 设置」可改输出格式 / 目录 / 并发 / DSD 目标规格 / 已存在文件策略 / 提示音。

## 审校修复记录（相对原始源码）

- 修复 24-bit DSD→FLAC 传参 `-sample_fmt s24` 导致 ffmpeg 报错退出的致命 Bug（改为 `-sample_fmt s32 -bits_per_raw_sample 24`）。
- 改用 `ProcessStartInfo.ArgumentList` 调用 `ffmpeg`/`ffprobe`，彻底避免中文/引号/特殊字符路径与元数据截断；数值统一 `InvariantCulture`；关闭冗余 stdout 重定向防死锁；出错时回显 stderr 关键报错行。
- 补全 `-metadata album` 专辑标签写入，去除 DSD→MP3 重复 `-ar` 参数。
- 修复 `CueCorrector` 不闭合左引号与右引号处理漏洞，并支持 `FILE` 行无引号空格文件名补全。
- 修复 `MainForm` 忽略“完成后播放提示音”开关的问题，补全进度条 `剩余 MM:SS`（ETA）显示与运行前自动重查 ffmpeg。
- 补全 `Processor` 多轨全零时间戳校验、单 FILE 拖入音频回退匹配、按输出目录长度动态计算的 Windows 255 字符路径截断、空标题回退警告，以及多候选音频按体积优先匹配。
- 设置对话框自定义 DSD 位深改为合法下拉选项（16 / 24 bit），防止输入非法位深导致转码失败。
