namespace ReaGui.Models;

/// <summary>命令参数的输入类型，决定表单控件与命令行拼装方式。</summary>
public enum ArgType
{
    Text,
    Int,
    Bool,
    Enum,
    FilePath,
}

/// <summary>
/// 单个命令参数（位置参数或命名标志）的元数据。
/// By leewheel 2026-10-16
/// </summary>
public sealed class CommandParameter
{
    /// <summary>命名标志名（不含前导 --），如 provider / procedure / pattern。</summary>
    public required string Name { get; init; }

    /// <summary>表单里显示的中文标签。</summary>
    public required string Label { get; init; }

    public ArgType Type { get; init; } = ArgType.Text;

    public bool Required { get; init; }

    /// <summary>作为位置参数时的序号（0 起）；null 表示只能作为命名标志。</summary>
    public int? Position { get; init; }

    /// <summary>枚举候选值。</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>表单下方的提示说明。</summary>
    public string? Hint { get; init; }

    /// <summary>默认值（枚举/文本）。</summary>
    public string? Default { get; init; }

    /// <summary>是否布尔开关（Type==Bool 时为 true 才追加 --Name）。</summary>
    public bool IsFlagOnly => Type == ArgType.Bool;

    /// <summary>下拉候选：枚举值或空。</summary>
    public IReadOnlyList<string> CandidateOptions => Options ?? Array.Empty<string>();
}

/// <summary>
/// 一条 REA CLI 命令的完整描述。GUI 依据它自动生成表单并拼装命令行。
/// </summary>
public sealed class ReaCommand
{
    /// <summary>CLI 命令名，如 decompile。</summary>
    public required string Name { get; init; }

    /// <summary>显示标题（中文）。</summary>
    public required string Title { get; init; }

    public required string Category { get; init; }

    public string Description { get; init; } = "";

    public IReadOnlyList<CommandParameter> Parameters { get; init; } = Array.Empty<CommandParameter>();

    /// <summary>是否属于核心逆向工作流（会被“核心分析”页快捷使用）。</summary>
    public bool IsCore { get; init; }

    public string Display => $"{Title}  ·  {Name}";
}
