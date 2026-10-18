using ReaGui.Models;

namespace ReaGui.Services;

/// <summary>
/// 依据命令元数据 + 表单值拼装 REA CLI 参数令牌。
/// 规则：位置参数按序输出；值以 '-' 开头且有对应命名标志时改用 --flag=value；
/// 布尔开关为真才追加 --Name；末尾统一附加全局 --json --full-output。
/// By leewheel 2026-10-16
/// </summary>
public static class ReaArgBuilder
{
    private static readonly HashSet<string> Truthy =
        new(StringComparer.OrdinalIgnoreCase) { "true", "1", "on", "yes", "y" };

    public static List<string> Build(ReaCommand cmd, Func<CommandParameter, string?> get)
    {
        var positional = new List<(int Pos, string Val)>();
        var flags = new List<string>();

        foreach (var p in cmd.Parameters)
        {
            var value = get(p)?.Trim();
            if (string.IsNullOrEmpty(value)) continue;

            if (p.Type == ArgType.Bool)
            {
                if (Truthy.Contains(value)) flags.Add("--" + p.Name);
                continue;
            }

            if (p.Position is int pos)
            {
                bool hasFlagEscape = p.Name != "path";
                if (value.StartsWith('-') && hasFlagEscape)
                    flags.Add($"--{p.Name}={value}");
                else
                    positional.Add((pos, value));
            }
            else
            {
                flags.Add($"--{p.Name}={value}");
            }
        }

        var tokens = new List<string> { cmd.Name };
        foreach (var (pos, val) in positional.OrderBy(x => x.Pos)) tokens.Add(val);
        tokens.AddRange(flags);
        tokens.Add("--json");
        tokens.Add("--full-output");
        return tokens;
    }

    /// <summary>生成人类可读的命令行预览（用于展示，不用于执行）。</summary>
    public static string Preview(ReaCommand cmd, Func<CommandParameter, string?> get, string nodeExe, string script)
    {
        var tokens = Build(cmd, get);
        var sb = new System.Text.StringBuilder();
        sb.Append(nodeExe).Append(' ').Append(Quote(script));
        foreach (var t in tokens)
        {
            sb.Append(' ');
            sb.Append(Quote(t));
        }
        return sb.ToString();
    }

    private static string Quote(string s) =>
        s.Any(char.IsWhiteSpace) || s.Length == 0 ? "\"" + s + "\"" : s;
}
