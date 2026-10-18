using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ReaGui.Models;
using ReaGui.Services;

namespace ReaGui.ViewModels;

/// <summary>
/// “设置”页：配置 REA 仓库根目录、node 路径、脚本路径与超时。
/// By leewheel 2026-10-16
/// </summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly MainViewModel _shell;

    public SettingsViewModel(MainViewModel shell)
    {
        _shell = shell;
        var s = SettingsService.Current;
        _repoRoot = s.RepoRoot;
        _nodePath = s.NodePath;
        _scriptRelativePath = s.ScriptRelativePath;
        _timeoutSeconds = s.TimeoutSeconds;
    }

    private string _repoRoot;
    public string RepoRoot { get => _repoRoot; set { if (Set(ref _repoRoot, value)) OnPropertyChanged(nameof(ScriptFullPreview)); } }

    private string _nodePath;
    public string NodePath { get => _nodePath; set => Set(ref _nodePath, value); }

    private string _scriptRelativePath;
    public string ScriptRelativePath { get => _scriptRelativePath; set { if (Set(ref _scriptRelativePath, value)) OnPropertyChanged(nameof(ScriptFullPreview)); } }

    private int _timeoutSeconds;
    public int TimeoutSeconds { get => _timeoutSeconds; set => Set(ref _timeoutSeconds, value); }

    public string ScriptFullPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RepoRoot)) return "(未设置仓库根目录)";
            var full = Path.Combine(RepoRoot, ScriptRelativePath);
            return File.Exists(full) ? full + "   ✓ 存在" : full + "   ✗ 未找到";
        }
    }

    private string _status = "";
    public string Status { get => _status; private set => Set(ref _status, value); }

    public ICommand BrowseRepoCommand => _browseRepo ??= new RelayCommand(BrowseRepo);
    private ICommand? _browseRepo;
    private void BrowseRepo()
    {
        var dlg = new OpenFolderDialog { Title = "选择 REA 仓库根目录" };
        if (Directory.Exists(RepoRoot)) dlg.InitialDirectory = RepoRoot;
        if (dlg.ShowDialog() == true)
        {
            RepoRoot = dlg.FolderName;
            Status = "已选择仓库目录，记得点“保存设置”。";
        }
    }

    public ICommand DetectCommand => _detect ??= new RelayCommand(Detect);
    private ICommand? _detect;
    private void Detect()
    {
        var root = SettingsService.DetectRepoRoot();
        if (root is not null)
        {
            RepoRoot = root;
            Status = "已自动探测到 REA 仓库: " + root;
        }
        else
        {
            Status = "未能自动探测到，请手动选择包含 scripts/rea.mjs 的目录。";
        }
    }

    public ICommand SaveCommand => _save ??= new RelayCommand(Save);
    private ICommand? _save;
    private void Save()
    {
        SettingsService.Save(new ReaSettings
        {
            RepoRoot = RepoRoot?.Trim() ?? "",
            NodePath = string.IsNullOrWhiteSpace(NodePath) ? "node" : NodePath.Trim(),
            ScriptRelativePath = string.IsNullOrWhiteSpace(ScriptRelativePath) ? "scripts/rea.mjs" : ScriptRelativePath.Trim(),
            TimeoutSeconds = TimeoutSeconds <= 0 ? 600 : TimeoutSeconds,
        });
        OnPropertyChanged(nameof(ScriptFullPreview));
        Status = "设置已保存到 %APPDATA%\\ReaGui\\settings.json。";
    }

    public ICommand TestCommand => _test ??= new AsyncRelayCommand(TestAsync, () => !_shell.Busy);
    private AsyncRelayCommand? _test;
    private async Task TestAsync()
    {
        Save();
        var cmd = CommandCatalog.Find("providers")!;
        Status = "正在测试连接（运行 providers）…";
        var result = await _shell.RunAsync(cmd, _ => null);
        Status = result.Success
            ? "✓ 连接正常：REA CLI 可被调用。"
            : "✗ 调用失败，请检查仓库路径与 node。详见“环境与诊断”页输出。";
    }
}
