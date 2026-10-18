using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReaGui.Services;

/// <summary>可持久化的运行配置。</summary>
public sealed class ReaSettings
{
    [JsonPropertyName("repoRoot")]
    public string RepoRoot { get; set; } = "";

    [JsonPropertyName("nodePath")]
    public string NodePath { get; set; } = "node";

    [JsonPropertyName("scriptRelativePath")]
    public string ScriptRelativePath { get; set; } = "scripts/rea.mjs";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 600;

    [JsonIgnore]
    public string ScriptFullPath => Path.Combine(RepoRoot, ScriptRelativePath);

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(RepoRoot) && File.Exists(ScriptFullPath);
}

/// <summary>
/// 设置读写：持久化到 %APPDATA%\ReaGui\settings.json，并自动探测 REA 仓库根目录。
/// By leewheel 2026-10-16
/// </summary>
public static class SettingsService
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReaGui");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static ReaSettings Current { get; private set; } = Load();

    private static ReaSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<ReaSettings>(File.ReadAllText(FilePath));
                if (s is not null)
                {
                    if (string.IsNullOrWhiteSpace(s.RepoRoot)) s.RepoRoot = DetectRepoRoot() ?? "";
                    return s;
                }
            }
        }
        catch
        {
            // 读取失败则回落到默认
        }
        return new ReaSettings { RepoRoot = DetectRepoRoot() ?? "" };
    }

    public static void Save(ReaSettings settings)
    {
        Current = settings;
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // 保存失败不阻断使用
        }
    }

    /// <summary>从可执行文件位置向上查找含 scripts/rea.mjs 的 REA 仓库根。</summary>
    public static string? DetectRepoRoot()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "scripts", "rea.mjs")) &&
                    File.Exists(Path.Combine(dir.FullName, "package.json")))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch
        {
            // 忽略
        }
        return null;
    }
}
