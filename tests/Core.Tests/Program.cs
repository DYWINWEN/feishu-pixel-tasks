using System.Text.Json;
using FeishuPixelTasks.Core;

var passed = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS " + name); passed++; }
void Reject(Action action, string name) { try { action(); } catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException) { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
var directory = Path.Combine(Path.GetTempPath(), "pixel-tests-" + Guid.NewGuid());
try
{
    var store = new StateStore(directory); var state = store.Load(); var service = new TaskService(store, state);
    service.SaveLocal(new("", "  中文像素任务  ", "2026-12-31", "https://example.com"));
    Check(store.Load().Tasks.Single().Summary == "中文像素任务", "Chinese task round-trip and title trimming");
    var local = state.Tasks.Single(); service.Complete(local.Guid);
    Check(store.Load().Tasks.Single().Status == "done", "local completion persists");
    service.Complete(local.Guid); Check(state.Tasks.Single().Status == "todo", "completed task can be restored");
    Check(File.Exists(store.PathName + ".bak"), "atomic writes retain previous-state backup");
    Reject(() => service.SaveLocal(new("", "x", "2026-02-31")), "invalid calendar dates rejected");
    Reject(() => service.SaveLocal(new("", "x", Url:"file:///C:/Windows/test.exe")), "executable/file URL blocked");
    Reject(() => service.SaveLocal(new("", "x", Url:"javascript:alert(1)")), "script URL blocked");
    Reject(() => DataRules.ImportSnapshot("{}"), "malformed snapshot does not become an empty task list");
    var imported = DataRules.ImportSnapshot("{\"tasks\":[{\"guid\":\"remote-id\",\"summary\":\"旧任务\",\"due\":\"\",\"url\":\"https://example.com\"}]}");
    Check(imported.Single().Source == "local" && imported.Single().Guid != "remote-id", "snapshot import cannot accidentally complete remote tasks");
    using var envelope = LarkClient.ParseEnvelope("{\"ok\":true,\"data\":{\"items\":[{\"guid\":\"12345678-1234-1234-1234-123456789012\",\"summary\":\"飞书测试\",\"due\":{\"timestamp\":\"1793980800000\"}}]}}");
    var remoteTask = LarkClient.ParseTasks(envelope.RootElement).Single();
    Check(remoteTask.Source == "feishu" && remoteTask.Due.Length == 10 && remoteTask.Url.StartsWith("https://applink.feishu.cn/"), "CLI task conversion");
    Reject(() => LarkClient.ParseEnvelope("{\"ok\":false,\"error\":\"token secret\"}"), "API failure never treated as success");
    using var invalid = LarkClient.ParseEnvelope("{\"ok\":true,\"data\":{}}");
    Reject(() => LarkClient.ParseTasks(invalid.RootElement), "missing items cannot erase cache");
    state.Tasks.Add(remoteTask);state.Settings = state.Settings with { Mode="larkcli" };
    service.Complete(remoteTask.Guid);service.Complete(remoteTask.Guid);
    Check(state.Pending.Count == 1 && state.Tasks.Contains(remoteTask), "remote completion queued once and remains visible until confirmed");
    var remote = new FakeRemote { Items=[remoteTask], FailComplete=true, FailFetch=true };
    try { await service.SyncAsync(remote); } catch (InvalidOperationException) { }
    Check(state.Tasks.Contains(remoteTask) && state.Pending.Single().Attempts == 1, "network failure retains cached tasks and pending completion");
    Check(store.Load().Pending.Count == 1, "failed completion survives restart");
    for(var i=0;i<4;i++) { try { await service.SyncAsync(remote); } catch (InvalidOperationException) { } }
    Check(state.Pending.Single().Attempts == 5, "repeated failures are never silently dropped");
    remote.FailComplete=false;
    try { await service.SyncAsync(remote); } catch (InvalidOperationException) { }
    Check(state.Pending.Count==0 && state.Tasks.All(t=>t.Guid!=remoteTask.Guid), "confirmed completion persists even if subsequent fetch fails");
    remote.FailFetch=false;remote.Items=[];await service.SyncAsync(remote);
    Check(state.Tasks.Count==1 && state.Tasks.Single().Source=="local", "successful remote refresh preserves local tasks");
    Check(state.LastSync!="", "last sync time only advances after successful fetch");
    state.Tasks.Add(remoteTask);service.Complete(remoteTask.Guid);remote.FailComplete=true;
    await service.SyncAsync(remote);
    Check(state.Pending.Count==0, "full successful listing reconciles an already completed or removed remote task");
    File.WriteAllText(store.PathName, "{}");Reject(()=>store.Load(), "missing state fields cannot reset existing data");
    File.WriteAllText(store.PathName, "broken");Reject(()=>store.Load(), "corrupted state is not silently reset");
    Console.WriteLine($"{passed} checks passed.");
}
finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }

sealed class FakeRemote : ITaskRemote
{
    public bool FailComplete, FailFetch;
    public List<TaskItem> Items = [];
    public Task CompleteAsync(string guid, CancellationToken ct) => FailComplete ? Task.FromException(new InvalidOperationException("offline")) : Task.CompletedTask;
    public Task<List<TaskItem>> FetchAsync(CancellationToken ct) => FailFetch ? Task.FromException<List<TaskItem>>(new InvalidOperationException("offline")) : Task.FromResult(Items);
}
