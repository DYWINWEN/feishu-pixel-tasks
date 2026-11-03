using System.Text;
using System.Text.Json;

namespace FeishuPixelTasks.Core;

public sealed class StateStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public string PathName => Path.Combine(DirectoryPath, "state.json");
    public AppState Load()
    {
        if (!File.Exists(PathName)) return new();
        // A malformed state is never silently replaced with an empty state.
        var text = File.ReadAllText(PathName);
        using var document = JsonDocument.Parse(text);
        foreach (var key in new[] { "version", "tasks", "pending", "settings" })
            if (!document.RootElement.TryGetProperty(key, out _)) throw new InvalidDataException("任务文件缺少必要字段：" + key);
        var state = JsonSerializer.Deserialize<AppState>(text, DataRules.Json)
            ?? throw new InvalidDataException("任务文件为空。");
        if (state.Version != 1 || state.Tasks is null || state.Pending is null || state.Settings is null)
            throw new InvalidDataException("任务文件版本或结构不支持。");
        DataRules.Validate(state.Settings);
        return state;
    }
    public void Save(AppState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = PathName + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, DataRules.Json));
            stream.Write(bytes);
            stream.Flush(true);
        }
        if (File.Exists(PathName)) File.Copy(PathName, PathName + ".bak", true);
        File.Move(temp, PathName, true);
    }
}
