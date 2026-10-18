using System.Diagnostics;
using System.IO;
using System.Text;
using ReaGui.Models;

namespace ReaGui.Services;

/// <summary>
/// 通过 node 调用 REA CLI（scripts/rea.mjs），捕获 stdout/stderr，支持取消与超时。
/// By leewheel 2026-10-16
/// </summary>
public sealed class ReaCliService
{
    public ReaSettings Settings => SettingsService.Current;

    public List<string> BuildTokens(ReaCommand cmd, Func<CommandParameter, string?> get)
        => ReaArgBuilder.Build(cmd, get);

    public string BuildPreview(ReaCommand cmd, Func<CommandParameter, string?> get)
        => ReaArgBuilder.Preview(cmd, get, Settings.NodePath, Settings.ScriptRelativePath);

    public Task<InvocationResult> RunAsync(ReaCommand cmd, Func<CommandParameter, string?> get, CancellationToken ct)
        => RunTokensAsync(BuildTokens(cmd, get), ct);

    public async Task<InvocationResult> RunTokensAsync(List<string> tokens, CancellationToken ct)
    {
        var commandLine = PreviewTokens(Settings.NodePath, Settings.ScriptRelativePath, tokens);

        if (!Settings.IsConfigured)
        {
            return Text(commandLine,
                "尚未配置 REA 仓库根目录，或找不到 scripts/rea.mjs。\n" +
                "请在“设置”里指定 REA 项目根目录（例如 D:\\REA\\Thisrea）。\n\n" +
                "当前探测路径: " + (string.IsNullOrWhiteSpace(Settings.RepoRoot) ? "(空)" : Settings.RepoRoot));
        }

        ProcessStartInfo psi;
        try
        {
            psi = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(Settings.NodePath) ? "node" : Settings.NodePath,
                WorkingDirectory = Settings.RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add(Path.GetFullPath(Settings.ScriptFullPath));
            foreach (var t in tokens) psi.ArgumentList.Add(t);
        }
        catch (Exception ex)
        {
            return Text(commandLine, "无法准备进程: " + ex.Message);
        }

        var sw = Stopwatch.StartNew();
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        int exitCode = -1;
        bool cancelled = false;
        bool timedOut = false;

        try
        {
            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var outTask = ReadAsync(proc.StandardOutput, stdout);
            var errTask = ReadAsync(proc.StandardError, stderr);

            using var timeoutCts = new CancellationTokenSource();
            if (Settings.TimeoutSeconds > 0)
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Settings.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            using var reg = linked.Token.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* 忽略 */ }
            });

            try
            {
                await proc.WaitForExitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
            }

            await Task.WhenAll(outTask, errTask);
            try { exitCode = proc.ExitCode; } catch { exitCode = -1; }
            sw.Stop();

            cancelled = ct.IsCancellationRequested;
            timedOut = !cancelled && timeoutCts.IsCancellationRequested;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Text(commandLine, "启动 node 失败: " + ex.Message +
                "\n（请确认已安装 Node.js 且“设置”中的 node 路径正确）");
        }

        return InvocationResult.Parse(commandLine, stdout.ToString(), stderr.ToString(),
            exitCode, cancelled, timedOut, sw.Elapsed);
    }

    private static async Task ReadAsync(StreamReader reader, StringBuilder sink)
    {
        // 逐行读取避免与 stderr 互相阻塞；输出用于展示，行尾统一为换行。
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (sink.Length > 0) sink.Append('\n');
            sink.Append(line);
        }
    }

    public static string PreviewTokens(string nodeExe, string script, IEnumerable<string> tokens)
    {
        var sb = new StringBuilder();
        sb.Append(nodeExe).Append(' ').Append(Quote(script));
        foreach (var t in tokens) { sb.Append(' ').Append(Quote(t)); }
        return sb.ToString();
    }

    private static string Quote(string s) =>
        s.Length == 0 || s.Any(char.IsWhiteSpace) ? "\"" + s + "\"" : s;

    private static InvocationResult Text(string commandLine, string message) => new()
    {
        CommandLine = commandLine,
        ExitCode = -1,
        StdErr = message,
        ParsedEnvelope = false,
    };
}
