using System.Text.Json;

namespace FeishuPixelTasks.Core;

public sealed record TaskItem(string Guid, string Summary, string Due = "", string Url = "", string Status = "todo", string Source = "local");
public sealed record PendingAction(string Guid, int Attempts = 0, string Error = "");
public sealed record Preferences(string Skin = "oak", int Opacity = 92, bool TopMost = false, string Mode = "local", string CliPath = "lark-cli", bool AutoSync = false);
public sealed class AppState
{
    public int Version { get; set; } = 1;
    public List<TaskItem> Tasks { get; set; } = [];
    public List<PendingAction> Pending { get; set; } = [];
    public Preferences Settings { get; set; } = new();
    public string LastSync { get; set; } = "";
    public string Notice { get; set; } = "本地模式 · 可在设置中连接飞书";
}

public static class DataRules
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static bool SafeUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme is "https" or "http";
    public static string Title(string title)
    {
        title = title.Trim();
        if (title.Length is 0 or > 3000) throw new InvalidDataException("任务标题须为 1–3000 字。");
        return title;
    }
    public static string Due(string value)
    {
        if (value != "" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", out _)) throw new InvalidDataException("截止日期格式应为 yyyy-MM-dd。");
        return value;
    }
    public static Preferences Validate(Preferences p)
    {
        if (p.Skin is not ("oak" or "slate") || p.Mode is not ("local" or "larkcli") || p.Opacity is < 40 or > 100)
            throw new InvalidDataException("设置值无效。");
        if (string.IsNullOrWhiteSpace(p.CliPath)) throw new InvalidDataException("请填写 lark-cli 路径。");
        return p;
    }
    // Old snapshots are imported as local copies. Importing never sends remote completion requests.
    public static List<TaskItem> ImportSnapshot(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("文件需要包含 tasks 数组。");
        if (tasks.GetArrayLength() > 10000) throw new InvalidDataException("任务过多，最多导入 10000 条。");
        return tasks.EnumerateArray().Select(t => {
            var url = t.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            if (url != "" && !SafeUrl(url)) throw new InvalidDataException("任务链接只支持 http/https。");
            return new TaskItem(System.Guid.NewGuid().ToString(), Title(t.GetProperty("summary").GetString() ?? ""),
                Due(t.TryGetProperty("due", out var d) ? d.GetString() ?? "" : ""), url,
                t.TryGetProperty("status", out var s) && s.GetString() == "done" ? "done" : "todo");
        }).ToList();
    }
}
