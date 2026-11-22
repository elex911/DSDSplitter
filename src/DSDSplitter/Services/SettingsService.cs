using System.Text.Json;
using System.Text.Json.Serialization;
using DSDSplitter.Models;

namespace DSDSplitter.Services;

/// <summary>
/// 读写 settings.json。
/// 便携优先：放 exe 同目录；若不可写（如 C:\Program Files）则回退 %APPDATA%\DSDSplitter。
/// 见开发文档 5 / 第 0 节 #8。
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public SettingsService()
    {
        _filePath = ResolveSettingsPath();
    }

    /// <summary>settings.json 实际所在路径（便携或 %APPDATA%）。</summary>
    public string FilePath => _filePath;

    public Settings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var s = JsonSerializer.Deserialize<Settings>(json, JsonOpts);
                if (s is not null) return s;
            }
        }
        catch
        {
            // 文件损坏或无读权限：静默回退默认值，不崩。
        }

        var def = new Settings();
        TrySave(def); // 首次运行自动生成
        return def;
    }

    public bool TrySave(Settings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOpts));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveSettingsPath()
    {
        // 1) 便携：exe 同目录可写就用它
        var exeDir = AppContext.BaseDirectory;
        if (CanWriteTo(exeDir))
            return Path.Combine(exeDir, "settings.json");

        // 2) 回退：%APPDATA%\DSDSplitter
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "DSDSplitter");
        return Path.Combine(dir, "settings.json");
    }

    private static bool CanWriteTo(string dir)
    {
        try
        {
            var test = Path.Combine(dir, $".writetest_{Guid.NewGuid():N}");
            using (var fs = File.Create(test, 1, FileOptions.DeleteOnClose)) { }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
