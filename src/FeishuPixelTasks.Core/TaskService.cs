namespace FeishuPixelTasks.Core;

public sealed class TaskService(StateStore store, AppState state)
{
    public AppState State { get; } = state;
    public void Persist() => store.Save(State);
    public void SaveLocal(TaskItem input)
    {
        var old = State.Tasks.Find(t => t.Guid == input.Guid);
        if (old is { Source: "feishu" }) throw new InvalidOperationException("飞书任务请在飞书中编辑。");
        if (input.Url != "" && !DataRules.SafeUrl(input.Url)) throw new InvalidDataException("链接只支持 http/https。");
        var item = new TaskItem(old?.Guid ?? Guid.NewGuid().ToString(), DataRules.Title(input.Summary),
            DataRules.Due(input.Due), input.Url, old?.Status ?? "todo");
        State.Tasks.RemoveAll(t => t.Guid == item.Guid); State.Tasks.Add(item); Persist();
    }
    public void DeleteLocal(string guid)
    {
        var item = State.Tasks.Single(t => t.Guid == guid);
        if (item.Source != "local") throw new InvalidOperationException("飞书任务请在飞书中删除。");
        State.Tasks.Remove(item); Persist();
    }
    public void Complete(string guid)
    {
        var item = State.Tasks.Single(t => t.Guid == guid);
        if (item.Source == "feishu")
        {
            if (State.Settings.Mode != "larkcli") throw new InvalidOperationException("请先在设置中连接飞书，再完成此任务。");
            if (State.Pending.All(p => p.Guid != guid)) State.Pending.Add(new(guid));
        }
        else State.Tasks[State.Tasks.IndexOf(item)] = item with { Status = item.Status == "done" ? "todo" : "done" };
        Persist();
    }
    public void CancelPending(string guid) { State.Pending.RemoveAll(p => p.Guid == guid); Persist(); }
    public async Task SyncAsync(ITaskRemote remote, CancellationToken ct = default)
    {
        int done = 0;
        foreach (var action in State.Pending.ToArray())
        {
            try
            {
                await remote.CompleteAsync(action.Guid, ct);
                State.Pending.RemoveAll(p => p.Guid == action.Guid);
                State.Tasks.RemoveAll(t => t.Source == "feishu" && t.Guid == action.Guid);
                done++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                State.Pending.RemoveAll(p => p.Guid == action.Guid);
                State.Pending.Add(action with { Attempts = action.Attempts + 1, Error = "完成失败，请检查登录/权限后重试" });
            }
            Persist(); // Successful remote actions are saved before any later fetch can fail.
        }
        try
        {
            var fresh = await remote.FetchAsync(ct);
            State.Tasks.RemoveAll(t => t.Source == "feishu");
            State.Tasks.AddRange(fresh);
            // A previous request may have succeeded before a timeout/crash. A complete,
            // successful pending-task listing is authoritative: don't keep retrying absent tasks.
            State.Pending.RemoveAll(p => fresh.All(t => t.Guid != p.Guid));
            State.LastSync = DateTimeOffset.Now.ToString("o");
            State.Notice = State.Pending.Count == 0 ? $"同步成功 · 本次完成 {done} 条" : $"同步完成 · {State.Pending.Count} 个动作待重试";
        }
        catch
        {
            State.Notice = "同步失败 · 已保留上次任务与待重试动作";
            Persist(); throw;
        }
        Persist();
    }
}
