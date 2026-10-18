using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ReaGui.Models;

namespace ReaGui.ViewModels;

/// <summary>命令面板里的一个分组。</summary>
public sealed class CategoryGroup
{
    public required string Name { get; init; }
    public required IReadOnlyList<ReaCommand> Commands { get; init; }
}

/// <summary>
/// 动态表单的一个字段，绑定自 <see cref="CommandParameter"/>。
/// By leewheel 2026-10-16
/// </summary>
public sealed class ParameterFieldViewModel : ObservableObject
{
    private readonly MainViewModel _shell;
    private readonly Action _onChanged;

    public ParameterFieldViewModel(CommandParameter param, MainViewModel shell, Action onChanged)
    {
        Param = param;
        _shell = shell;
        _onChanged = onChanged;
        _value = InitialValue(param, shell);
    }

    public CommandParameter Param { get; }

    public string Name => Param.Name;
    public string Label => Param.Label;
    public string Hint => Param.Hint ?? "";
    public ArgType Type => Param.Type;
    public bool IsBool => Param.Type == ArgType.Bool;
    public bool IsEnum => Param.Type == ArgType.Enum;
    public bool IsTextLike => !IsBool;
    public bool IsFilePath => Param.Type == ArgType.FilePath;
    public bool Required => Param.Required;
    public IReadOnlyList<string> Options => Param.CandidateOptions;

    // 表单控件可见性
    public bool ShowBool => Param.Type == ArgType.Bool;
    public bool ShowEnum => Param.Type == ArgType.Enum;
    public bool ShowFilePath => Param.Type == ArgType.FilePath;
    public bool ShowText => Param.Type is ArgType.Text or ArgType.Int;

    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value))
            {
                OnPropertyChanged(nameof(BoolValue));
                _onChanged();
            }
        }
    }

    public bool BoolValue
    {
        get => Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        set { Value = value ? "true" : "false"; }
    }

    public ICommand BrowseCommand => _browse ??= new RelayCommand(Browse, () => IsFilePath);
    private ICommand? _browse;
    private void Browse()
    {
        var dlg = new OpenFileDialog { Title = "选择 " + Label, Filter = "所有文件 (*.*)|*.*" };
        if (File.Exists(Value))
        {
            dlg.FileName = Path.GetFileName(Value);
            dlg.InitialDirectory = Path.GetDirectoryName(Value);
        }
        if (dlg.ShowDialog() == true) Value = dlg.FileName;
    }

    private static string InitialValue(CommandParameter p, MainViewModel shell) => p.Name switch
    {
        "path" => shell.TargetPath,
        "provider" => string.IsNullOrWhiteSpace(shell.Provider) ? (p.Default ?? "auto") : shell.Provider,
        "snapshot" => shell.SnapshotPath,
        _ => p.Default ?? "",
    };
}

/// <summary>
/// “命令面板”页：元数据驱动的通用命令运行器（可扩展）。
/// </summary>
public sealed class RunnerViewModel : ObservableObject
{
    private readonly MainViewModel _shell;

    public RunnerViewModel(MainViewModel shell)
    {
        _shell = shell;
        _shell.RunAvailabilityChanged += () => RunCommand.RaiseCanExecuteChanged();
        Groups = CommandCatalog.Categories
            .Select(cat => new CategoryGroup { Name = cat, Commands = CommandCatalog.InCategory(cat).ToList() })
            .ToList();
        SelectedCommand = Groups.SelectMany(g => g.Commands).FirstOrDefault()!;
    }

    public IReadOnlyList<CategoryGroup> Groups { get; }

    private ReaCommand? _selectedCommand;
    public ReaCommand? SelectedCommand
    {
        get => _selectedCommand;
        set
        {
            if (Set(ref _selectedCommand, value) && value is not null)
            {
                BuildFields(value);
            }
        }
    }

    public ObservableCollection<ParameterFieldViewModel> Fields { get; } = new();

    private void BuildFields(ReaCommand cmd)
    {
        Fields.Clear();
        foreach (var p in cmd.Parameters)
            Fields.Add(new ParameterFieldViewModel(p, _shell, RefreshPreview));
        RefreshPreview();
    }

    private string? Get(string name) => Fields.FirstOrDefault(f => f.Name == name)?.Value;

    private string _preview = "";
    public string Preview { get => _preview; private set => Set(ref _preview, value); }

    public void RefreshPreview()
    {
        if (_selectedCommand is null) { Preview = ""; return; }
        Preview = _shell.Cli.BuildPreview(_selectedCommand, p => Get(p.Name));
    }

    private string _resultText = "";
    public string ResultText { get => _resultText; private set { if (Set(ref _resultText, value)) OnPropertyChanged(nameof(HasResult)); } }
    public bool HasResult => !string.IsNullOrEmpty(_resultText);

    private string _resultTitle = "";
    public string ResultTitle { get => _resultTitle; private set => Set(ref _resultTitle, value); }

    public AsyncRelayCommand RunCommand => _run ??= new AsyncRelayCommand(RunAsync, () => !_shell.Busy && _selectedCommand is not null);
    private AsyncRelayCommand? _run;

    private async Task RunAsync()
    {
        if (_selectedCommand is null) return;
        ResultTitle = _selectedCommand.Title + " · " + _selectedCommand.Name;
        var result = await _shell.RunAsync(_selectedCommand, p => Get(p.Name));
        ResultText = result.ViewerText();
    }
}
