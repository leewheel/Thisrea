using System.Windows.Input;
using ReaGui.Models;

namespace ReaGui.ViewModels;

/// <summary>
/// “核心分析”页：共享目标/引擎，快捷选择分析命令并展示结果。
/// By leewheel 2026-10-16
/// </summary>
public sealed class AnalysisViewModel : ObservableObject
{
    private readonly MainViewModel _shell;

    public AnalysisViewModel(MainViewModel shell)
    {
        _shell = shell;
        _shell.TargetChanged += RefreshPreview;
        _shell.RunAvailabilityChanged += () => RunCommand.RaiseCanExecuteChanged();
        _coreCommands = CommandCatalog.Core;
        _selectedCommand = _coreCommands[0];
    }

    private readonly IReadOnlyList<ReaCommand> _coreCommands;
    public IReadOnlyList<ReaCommand> CoreCommands => _coreCommands;

    private ReaCommand _selectedCommand;
    public ReaCommand SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            if (Set(ref _selectedCommand, value))
            {
                OnPropertyChanged(nameof(ShowSelector));
                OnPropertyChanged(nameof(SelectorLabel));
                OnPropertyChanged(nameof(ShowSearchOptions));
                RefreshPreview();
            }
        }
    }

    // 位置参数（函数名/地址/搜索词/追踪查询）
    private string _selectorValue = "";
    public string SelectorValue
    {
        get => _selectorValue;
        set { if (Set(ref _selectorValue, value)) RefreshPreview(); }
    }

    private string _searchKind = "strings";
    public string SearchKind { get => _searchKind; set { if (Set(ref _searchKind, value)) RefreshPreview(); } }

    private string _searchMode = "literal";
    public string SearchMode { get => _searchMode; set { if (Set(ref _searchMode, value)) RefreshPreview(); } }

    private bool _caseSensitive;
    public bool CaseSensitive { get => _caseSensitive; set { if (Set(ref _caseSensitive, value)) RefreshPreview(); } }

    private string _fileOffset = "";
    public string FileOffset { get => _fileOffset; set { if (Set(ref _fileOffset, value)) RefreshPreview(); } }

    /// <summary>当前命令是否有第二个位置参数（选择器）。</summary>
    public bool ShowSelector => SelectedCommand.Parameters.Any(p => p.Position == 1);

    public string SelectorLabel =>
        SelectedCommand.Parameters.FirstOrDefault(p => p.Position == 1)?.Label ?? "选择器";

    public bool ShowSearchOptions => SelectedCommand.Name is "search";
    public bool ShowFileOffset => SelectedCommand.Name is "decompile";

    private string _resultText = "";
    public string ResultText { get => _resultText; private set { if (Set(ref _resultText, value)) OnPropertyChanged(nameof(HasResult)); } }
    public bool HasResult => !string.IsNullOrEmpty(_resultText);

    private string _resultTitle = "";
    public string ResultTitle { get => _resultTitle; private set => Set(ref _resultTitle, value); }

    // ---- 命令行预览 ----
    private string _preview = "";
    public string Preview { get => _preview; private set => Set(ref _preview, value); }

    private Dictionary<string, string?> BuildValues() => new()
    {
        ["path"] = _shell.TargetPath,
        ["provider"] = _shell.Provider,
        ["snapshot"] = _shell.SnapshotPath,
        ["procedure"] = SelectorValue,
        ["address"] = SelectorValue,
        ["pattern"] = SelectorValue,
        ["query"] = SelectorValue,
        ["kind"] = SearchKind,
        ["mode"] = SearchMode,
        ["case-sensitive"] = CaseSensitive ? "true" : null,
        ["file-offset"] = FileOffset,
    };

    public void RefreshPreview()
    {
        var values = BuildValues();
        Preview = _shell.Cli.BuildPreview(SelectedCommand, p => values.TryGetValue(p.Name, out var v) ? v : null);
    }

    public AsyncRelayCommand RunCommand => _run ??= new AsyncRelayCommand(RunAsync, () => !_shell.Busy);
    private AsyncRelayCommand? _run;

    private async Task RunAsync()
    {
        var values = BuildValues();
        ResultTitle = SelectedCommand.Title + " · " + SelectedCommand.Name;
        var result = await _shell.RunAsync(SelectedCommand, p => values.TryGetValue(p.Name, out var v) ? v : null);
        ResultText = result.ViewerText();
    }
}
