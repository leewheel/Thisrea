using System.Windows.Input;
using ReaGui.Models;

namespace ReaGui.ViewModels;

/// <summary>
/// “环境与诊断”页：运行 providers / capabilities / doctor / setup 等状态命令。
/// By leewheel 2026-10-16
/// </summary>
public sealed class EnvironmentViewModel : ObservableObject
{
    private readonly MainViewModel _shell;

    public EnvironmentViewModel(MainViewModel shell)
    {
        _shell = shell;
        _shell.RunAvailabilityChanged += () =>
        {
            ProvidersCommand.RaiseCanExecuteChanged();
            CapabilitiesCommand.RaiseCanExecuteChanged();
            DoctorCommand.RaiseCanExecuteChanged();
            SetupCommand.RaiseCanExecuteChanged();
        };
    }

    public IReadOnlyList<string> DoctorProviders => CommandCatalog.DoctorProviders;

    /// <summary>doctor 的引擎下拉：含一个空选项表示“未指定”。</summary>
    public IReadOnlyList<string> DoctorProviderOptions { get; } =
        new[] { "" }.Concat(CommandCatalog.DoctorProviders).ToList();

    private string _doctorTarget = "";
    public string DoctorTarget { get => _doctorTarget; set => Set(ref _doctorTarget, value); }

    private string _doctorProvider = "";
    public string DoctorProvider { get => _doctorProvider; set => Set(ref _doctorProvider, value); }

    private bool _setupDryRun = true;
    public bool SetupDryRun { get => _setupDryRun; set => Set(ref _setupDryRun, value); }

    private bool _setupYes;
    public bool SetupYes { get => _setupYes; set => Set(ref _setupYes, value); }

    private string _resultText = "";
    public string ResultText { get => _resultText; private set { if (Set(ref _resultText, value)) OnPropertyChanged(nameof(HasResult)); } }
    public bool HasResult => !string.IsNullOrEmpty(_resultText);

    private string _resultTitle = "";
    public string ResultTitle { get => _resultTitle; private set => Set(ref _resultTitle, value); }

    private async Task Run(string commandName, Func<CommandParameter, string?> get, string title)
    {
        var cmd = CommandCatalog.Find(commandName)!;
        ResultTitle = title;
        var result = await _shell.RunAsync(cmd, get);
        ResultText = result.ViewerText();
    }

    public AsyncRelayCommand ProvidersCommand => _providers ??= new AsyncRelayCommand(
        () => Run("providers", _ => null, "已配置引擎 · providers"), () => !_shell.Busy);
    private AsyncRelayCommand? _providers;

    public AsyncRelayCommand CapabilitiesCommand => _capabilities ??= new AsyncRelayCommand(
        () => Run("capabilities", _ => null, "引擎能力 · capabilities"), () => !_shell.Busy);
    private AsyncRelayCommand? _capabilities;

    public AsyncRelayCommand DoctorCommand => _doctor ??= new AsyncRelayCommand(
        () => Run("doctor", p => p.Name switch
        {
            "target" => DoctorTarget,
            "provider" => string.IsNullOrWhiteSpace(DoctorProvider) ? null : DoctorProvider,
            _ => null,
        }, "就绪诊断 · doctor"), () => !_shell.Busy);
    private AsyncRelayCommand? _doctor;

    public AsyncRelayCommand SetupCommand => _setup ??= new AsyncRelayCommand(
        () => Run("setup", p => p.Name switch
        {
            "dry-run" => SetupDryRun ? "true" : null,
            "yes" => SetupYes ? "true" : null,
            _ => null,
        }, "环境配置 · setup"), () => !_shell.Busy);
    private AsyncRelayCommand? _setup;
}
