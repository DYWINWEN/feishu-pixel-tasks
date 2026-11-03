using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FeishuPixelTasks.Core;

public interface ITaskRemote
{
    Task<List<TaskItem>> FetchAsync(CancellationToken ct);
    Task CompleteAsync(string guid, CancellationToken ct);
}

public sealed class LarkClient(string executable, IReadOnlyDictionary<string, string> credentials) : ITaskRemote
{
    private async Task<JsonDocument> RunAsync(string[] args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        foreach (var (key, value) in credentials) psi.Environment[key] = value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var process = new Process { StartInfo = psi };
        try { process.Start(); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { throw new InvalidOperationException("无法启动 lark-cli，请在设置中选择可用的 lark-cli.exe。", ex); }
        // Drain both streams concurrently: neither a full stderr pipe nor network delay can freeze the UI.
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        string output;
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            output = await stdout;
            await stderr;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { await Task.WhenAll(stdout, stderr); } catch (OperationCanceledException) { }
            throw new TimeoutException("飞书请求超时，缓存和未完成动作已保留。");
        }
        // Never echo CLI output: authentication diagnostics can contain credentials.
        if (process.ExitCode != 0) throw new InvalidOperationException("飞书调用失败，请检查 CLI 登录、令牌有效期和任务权限。");
        return ParseEnvelope(output);
    }
    public static JsonDocument ParseEnvelope(string output)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(output.Trim().TrimStart('\uFEFF')); }
        catch (JsonException) { throw new InvalidDataException("CLI 返回内容不是有效 JSON，原任务已保留。"); }
        if (!doc.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
        { doc.Dispose(); throw new InvalidOperationException("飞书未确认成功，请检查令牌或权限后重试。"); }
        return doc;
    }
    public static List<TaskItem> ParseTasks(JsonElement envelope)
    {
        if (!envelope.TryGetProperty("data", out var data) || !data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("飞书响应缺少任务列表，未覆盖本地缓存。");
        var result = new List<TaskItem>();
        foreach (var item in items.EnumerateArray())
        {
            var guid = item.GetProperty("guid").GetString() ?? "";
            if (!System.Guid.TryParse(guid, out _)) throw new InvalidDataException("飞书任务 ID 无效。");
            var due = "";
            if (item.TryGetProperty("due", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("timestamp", out var ts))
            {
                var raw = ts.ValueKind == JsonValueKind.String ? ts.GetString() : ts.GetRawText();
                if (long.TryParse(raw, out var ms) && ms > 0)
                    due = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd");
            }
            result.Add(new(guid, DataRules.Title(item.GetProperty("summary").GetString() ?? ""), due,
                "https://applink.feishu.cn/client/todo/detail?guid=" + Uri.EscapeDataString(guid), "todo", "feishu"));
        }
        return result.DistinctBy(t => t.Guid).ToList();
    }
    public async Task<List<TaskItem>> FetchAsync(CancellationToken ct)
    {
        using var doc = await RunAsync(["task", "+get-related-tasks", "--include-complete=false", "--page-all"], ct);
        return ParseTasks(doc.RootElement);
    }
    public async Task CompleteAsync(string guid, CancellationToken ct)
    {
        if (!System.Guid.TryParse(guid, out _)) throw new InvalidDataException("无效任务 ID。");
        using var doc = await RunAsync(["task", "+complete", "--task-id", guid], ct);
    }
}
