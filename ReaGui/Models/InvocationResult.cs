using System.Text;
using System.Text.Json;

namespace ReaGui.Models;

/// <summary>
/// 一次 CLI 调用的结果，含 REA 的 {ok,data|error,meta} 信封解析。
/// By leewheel 2026-10-16
/// </summary>
public sealed class InvocationResult
{
    public required string CommandLine { get; init; }
    public int ExitCode { get; init; }
    public string StdOut { get; init; } = "";
    public string StdErr { get; init; } = "";
    public bool Cancelled { get; init; }
    public bool TimedOut { get; init; }
    public TimeSpan Duration { get; init; }

    // 信封解析结果
    public bool ParsedEnvelope { get; init; }
    public bool? Ok { get; init; }
    public string? DataJson { get; init; }
    public string? ErrorJson { get; init; }
    public string? MetaJson { get; init; }

    public bool Success => Ok == true;

    /// <summary>把 JSON 元素美化缩进。</summary>
    public static string Pretty(JsonElement el) =>
        JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>供代码查看器展示的文本。</summary>
    public string ViewerText()
    {
        if (Cancelled) return "⚠ 调用已取消。";
        if (TimedOut) return "⚠ 调用超时。";

        if (!ParsedEnvelope)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(StdOut)) sb.AppendLine(StdOut.Trim());
            if (!string.IsNullOrWhiteSpace(StdErr))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("── stderr ──").AppendLine(StdErr.Trim());
            }
            if (sb.Length == 0) sb.AppendLine($"（无输出，退出码 {ExitCode}）");
            return sb.ToString();
        }

        var text = new StringBuilder();
        if (Ok == true)
        {
            text.AppendLine("// ✓ 成功  ·  退出码 " + ExitCode + "  ·  用时 " + Duration.TotalSeconds.ToString("0.0") + "s");
            text.AppendLine(DataJson ?? "{}");
        }
        else
        {
            text.AppendLine("// ✗ 失败  ·  退出码 " + ExitCode);
            text.AppendLine(ErrorJson ?? "{}");
            if (!string.IsNullOrWhiteSpace(StdErr))
                text.AppendLine().AppendLine("── stderr ──").AppendLine(StdErr.Trim());
        }
        return text.ToString();
    }

    /// <summary>解析 stdout 中的 REA 信封。</summary>
    public static InvocationResult Parse(string commandLine, string stdout, string stderr,
        int exitCode, bool cancelled, bool timedOut, TimeSpan duration)
    {
        bool parsed = false; bool? ok = null;
        string? data = null, error = null, meta = null;
        var trimmed = stdout.Trim();
        if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("ok", out var okEl))
                {
                    parsed = true;
                    ok = okEl.ValueKind == JsonValueKind.True;
                    if (doc.RootElement.TryGetProperty("data", out var d)) data = Pretty(d);
                    if (doc.RootElement.TryGetProperty("error", out var e)) error = Pretty(e);
                    if (doc.RootElement.TryGetProperty("meta", out var m)) meta = Pretty(m);
                }
            }
            catch (JsonException)
            {
                // 非合法 JSON，落到原始文本展示
            }
        }
        return new InvocationResult
        {
            CommandLine = commandLine,
            ExitCode = exitCode,
            StdOut = stdout,
            StdErr = stderr,
            Cancelled = cancelled,
            TimedOut = timedOut,
            Duration = duration,
            ParsedEnvelope = parsed,
            Ok = ok,
            DataJson = data,
            ErrorJson = error,
            MetaJson = meta,
        };
    }
}
