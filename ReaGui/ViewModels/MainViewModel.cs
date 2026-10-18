using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ReaGui.Models;
using ReaGui.Services;

namespace ReaGui.ViewModels;

/// <summary>导航页标识。</summary>
public enum NavPage { Analysis, Runner, Environment, Settings }

/// <summary>
/// 主窗口视图模型：持有共享目标/引擎状态、运行与取消、状态栏。
/// By leewheel 2026-10-16
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    public ReaCliService Cli { get; } = new();

    public AnalysisViewModel Analysis { get; }
    public RunnerViewModel Runner { get; }
    public EnvironmentViewModel Environment { get; }
    public SettingsViewModel Settings { get; }

    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        Analysis = new AnalysisViewModel(this);
        Runner = new RunnerViewModel(this);
        Environment = new EnvironmentViewModel(this);
        Settings = new SettingsViewModel(this);
    }

    // ---- 导航 ----
    public sealed record NavItem(NavPage Page, string Label, string Glyph);

    public IReadOnlyList<NavItem> NavItems { get; } = new[]
    {
        new NavItem(NavPage.Analysis, "核心分析", "▸"),
        new NavItem(NavPage.Runner, "命令面板", "≣"),
        new NavItem(NavPage.Environment, "环境诊断", "◈"),
    };

    public IReadOnlyList<string> ProviderOptions => Models.CommandCatalog.Providers;

    private NavPage _currentPage = NavPage.Analysis;
    public NavPage CurrentPage
    {
        get => _currentPage;
        set => Set(ref _currentPage, value);
    }

    public void GoAnalysis() => CurrentPage = NavPage.Analysis;
    public void GoRunner() => CurrentPage = NavPage.Runner;
    public void GoEnvironment() => CurrentPage = NavPage.Environment;
    public void GoSettings() => CurrentPage = NavPage.Settings;

    // ---- 共享目标状态 ----
    private string _targetPath = "";
    public string TargetPath
    {
        get => _targetPath;
        set { if (Set(ref _targetPath, value)) RaiseTargetChanged(); }
    }

    private string _provider = "auto";
    public string Provider
    {
        get => _provider;
        set { if (Set(ref _provider, value)) RaiseTargetChanged(); }
    }

    private string _snapshotPath = "";
    public string SnapshotPath
    {
        get => _snapshotPath;
        set { if (Set(ref _snapshotPath, value)) RaiseTargetChanged(); }
    }

    /// <summary>目标/引擎/快照变化时通知子视图刷新命令行预览。</summary>
    public event Action? TargetChanged;
    private void RaiseTargetChanged() => TargetChanged?.Invoke();

    public ICommand BrowseTargetCommand => _browseTarget ??= new RelayCommand(BrowseTarget);
    private ICommand? _browseTarget;
    private void BrowseTarget()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择要逆向的目标文件",
            Filter = "所有文件 (*.*)|*.*",
            CheckFileExists = true,
        };
        if (File.Exists(TargetPath))
        {
            dlg.FileName = Path.GetFileName(TargetPath);
            dlg.InitialDirectory = Path.GetDirectoryName(TargetPath);
        }
        if (dlg.ShowDialog() == true)
            TargetPath = dlg.FileName;
    }

    // ---- 运行状态 ----
    private bool _busy;
    public bool Busy
    {
        get => _busy;
        set { if (Set(ref _busy, value)) { CancelCommand.RaiseCanExecuteChanged(); RunAvailabilityChanged?.Invoke(); } }
    }

    private string _statusText = "就绪";
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    private string _lastCommandLine = "";
    public string LastCommandLine { get => _lastCommandLine; private set => Set(ref _lastCommandLine, value); }

    /// <summary>子视图可订阅以刷新自身 Run 按钮可用性。</summary>
    public event Action? RunAvailabilityChanged;

    public AsyncRelayCommand CancelCommand => _cancel ??= new AsyncRelayCommand(CancelAsync, () => Busy);
    private AsyncRelayCommand? _cancel;
    private Task CancelAsync()
    {
        _cts?.Cancel();
        StatusText = "正在取消…";
        return Task.CompletedTask;
    }

    /// <summary>集中执行：管理忙碌态、取消、状态栏，返回结果。</summary>
    public async Task<InvocationResult> RunAsync(ReaCommand cmd, Func<CommandParameter, string?> get)
    {
        if (Busy) return InvocationResult.Parse("", "", "已有任务在运行，请稍候。", -1, false, false, TimeSpan.Zero);

        _cts = new CancellationTokenSource();
        Busy = true;
        StatusText = "运行中 · " + cmd.Name + " …";
        LastCommandLine = Cli.BuildPreview(cmd, get);

        InvocationResult result;
        try
        {
            result = await Cli.RunAsync(cmd, get, _cts.Token);
        }
        finally
        {
            Busy = false;
            _cts.Dispose();
            _cts = null;
        }

        StatusText = result.Success
            ? $"完成 · {cmd.Name} · {result.Duration.TotalSeconds:0.0}s"
            : result.Cancelled
                ? "已取消"
                : $"失败 · {cmd.Name} · 退出码 {result.ExitCode}";

        return result;
    }
}
