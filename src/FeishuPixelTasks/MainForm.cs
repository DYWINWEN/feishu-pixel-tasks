using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FeishuPixelTasks.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FeishuPixelTasks;

internal sealed class MainForm : Form
{
    private const string Origin = "https://appassets.example";
    private readonly WebView2 web = new() { Dock = DockStyle.Fill };
    private readonly TaskService service;
    private readonly StateStore store;
    private readonly CredentialVault vault;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 300000 };
    private readonly string? smoke;
    private bool busy, ready, smokeStarted, folded;
    private int expandedHeight = 830;
    private Point? dragStart;

    public MainForm(string? smokePath)
    {
        smoke = smokePath;
        var folder = smoke is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FeishuPixelTasks") : Path.Combine(smoke, "test-data");
        store = new(folder); vault = new(folder);
        service = new(store, store.Load());
        if (smoke is not null)
        {
            service.State.Tasks = [new("demo-1", "整理项目阶段性成果", "2026-12-15"), new("demo-2", "更新团队任务清单", "2026-12-20"), new("demo-3", "汇总本月工作进展，并安排下一阶段的协作计划。")];
        }
        Text = "飞书像素任务"; FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.Dpi; MinimumSize = new Size(420, 380); Size = new Size(680, 830);
        StartPosition = FormStartPosition.CenterScreen;
        var area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        BackColor = Color.FromArgb(27, 32, 42);
        Controls.Add(web); ApplyPreferences();
        Shown += async (_, _) => await InitializeAsync();
        timer.Tick += async (_, _) => { if (!busy && ready && service.State.Settings is { AutoSync: true, Mode: "larkcli" }) await RefreshAsync(); };
        FormClosing += (_, _) => { lifetime.Cancel(); timer.Stop(); };
        FormClosed += (_, _) => { web.Dispose(); timer.Dispose(); };
    }
    private async Task InitializeAsync()
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(store.DirectoryPath, "WebView2"));
            await web.EnsureCoreWebView2Async(environment);
            web.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.example", Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.DenyCors);
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            web.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            web.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.CoreWebView2.NavigationStarting += (_, e) => e.Cancel = !e.Uri.StartsWith(Origin + "/", StringComparison.Ordinal);
            web.CoreWebView2.WebMessageReceived += async (_, e) =>
            {
                if (!e.Source.StartsWith(Origin + "/", StringComparison.Ordinal)) return;
                try { await HandleAsync(e.WebMessageAsJson); }
                catch (Exception ex) { ShowError(ex); }
            };
            web.CoreWebView2.Navigate(Origin + "/index.html");
            timer.Start();
        }
        catch (Exception ex)
        {
            if (smoke is not null) { Directory.CreateDirectory(smoke); File.WriteAllText(Path.Combine(smoke, "error.txt"), ex.ToString()); Environment.ExitCode = 1; Close(); }
            else MessageBox.Show(this, "WebView2 初始化失败。请安装或修复 Microsoft Edge WebView2 Runtime。\n" + ex.Message, Text);
        }
    }
    private void Post(object message) { if (!IsDisposed && web.CoreWebView2 is not null) web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, DataRules.Json)); }
    private void PushState() => Post(new { type = "state", state = service.State, busy, hasCredentials = vault.Exists, desktop = true });
    private void ShowError(Exception ex)
    {
        if (IsDisposed || lifetime.IsCancellationRequested) return;
        var message = ex is InvalidDataException or InvalidOperationException or TimeoutException ? ex.Message : "操作失败，原数据已保留。请检查文件权限、网络及配置。";
        Post(new { type = "error", message }); PushState();
    }
    private void ApplyPreferences() { Opacity = service.State.Settings.Opacity / 100.0; TopMost = service.State.Settings.TopMost; }
    private async Task RefreshAsync()
    {
        if (!await gate.WaitAsync(0)) return;
        busy = true; PushState();
        try
        {
            if (service.State.Settings.Mode == "larkcli")
                await service.SyncAsync(new LarkClient(service.State.Settings.CliPath, vault.Read()), lifetime.Token);
            else { service.State.Notice = "本地任务已保存 · 连接飞书请打开设置"; service.Persist(); }
        }
        catch (Exception ex) { ShowError(ex); }
        finally { busy = false; gate.Release(); PushState(); }
    }
    private string? PickFile(string title, string filter)
    {
        using var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }
    private async Task HandleAsync(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var m = doc.RootElement;
        var type = m.GetProperty("type").GetString();
        switch (type)
        {
            case "ready": ready = true; PushState(); return;
            case "rendered": if (smoke is not null && !smokeStarted) { smokeStarted = true; await SmokeAsync(); } return;
            case "close": Close(); return;
            case "minimize": WindowState = FormWindowState.Minimized; return;
            case "drag-start": dragStart = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top); return;
            case "drag-move": if (dragStart is Point p) Location = new Point(Cursor.Position.X - p.X, Cursor.Position.Y - p.Y); return;
            case "drag-end": dragStart = null; return;
            case "resize": ReleaseCapture(); SendMessage(Handle, 0xA1, 17, 0); return;
            case "fold":
                folded = !folded;
                if (folded) { expandedHeight = Height; MinimumSize = new Size(420, 70); Height = 70; }
                else { MinimumSize = new Size(420, 380); Height = expandedHeight; }
                return;
            case "open":
                var task = service.State.Tasks.Single(t => t.Guid == m.GetProperty("guid").GetString());
                if (!DataRules.SafeUrl(task.Url)) throw new InvalidDataException("此任务没有可打开的网页链接。");
                Process.Start(new ProcessStartInfo(task.Url) { UseShellExecute = true }); return;
            case "refresh": await RefreshAsync(); return;
        }
        if (busy) throw new InvalidOperationException("正在同步，请稍后再修改任务或设置。");
        switch (type)
        {
            case "settings":
                service.State.Settings = DataRules.Validate(m.GetProperty("settings").Deserialize<Preferences>(DataRules.Json)!);
                ApplyPreferences(); service.Persist(); break;
            case "save-task": service.SaveLocal(m.GetProperty("task").Deserialize<TaskItem>(DataRules.Json)!); break;
            case "delete-task": service.DeleteLocal(m.GetProperty("guid").GetString()!); break;
            case "complete":
                service.Complete(m.GetProperty("guid").GetString()!); PushState();
                if (service.State.Settings.Mode == "larkcli") await RefreshAsync(); break;
            case "cancel-pending": service.CancelPending(m.GetProperty("guid").GetString()!); break;
            case "import":
                var path = PickFile("导入旧版 tasks_snapshot.json（作为本地副本）", "JSON 文件|*.json");
                if (path is not null)
                {
                    if (new FileInfo(path).Length > 8_000_000) throw new InvalidDataException("导入文件不能超过 8 MB。");
                    service.State.Tasks.AddRange(DataRules.ImportSnapshot(File.ReadAllText(path)));
                    service.State.Notice = "已导入为本地任务副本"; service.Persist();
                }
                break;
            case "export":
                using (var dialog = new SaveFileDialog { Title = "导出任务（不含凭据）", Filter = "JSON 文件|*.json", FileName = "tasks-export.json" })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new { tasks = service.State.Tasks }, DataRules.Json));
                break;
            case "choose-cli":
                var cli = PickFile("选择 lark-cli.exe", "可执行文件|*.exe");
                if (cli is not null) { service.State.Settings = service.State.Settings with { CliPath = cli }; service.Persist(); }
                break;
            case "import-credentials":
                var token = PickFile("导入 CLI 凭据文件（仅加密保存在本机）", "JSON 文件|*.json");
                if (token is not null) { vault.Import(token); service.State.Notice = "凭据已加密保存 · 可点击刷新验证"; service.Persist(); }
                break;
            case "clear-credentials": vault.Clear(); break;
        }
        PushState();
    }
    private async Task SmokeAsync()
    {
        try
        {
            Directory.CreateDirectory(smoke!);
            var result = await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({cards:document.querySelectorAll('.task').length,font:document.fonts.check('24px Pixel'),overflow:document.documentElement.scrollWidth>innerWidth})");
            File.WriteAllText(Path.Combine(smoke!, "webview-check.json"), result);
            using var check = JsonDocument.Parse(JsonSerializer.Deserialize<string>(result)!);
            if (check.RootElement.GetProperty("cards").GetInt32() != 3 || !check.RootElement.GetProperty("font").GetBoolean() || check.RootElement.GetProperty("overflow").GetBoolean())
                throw new InvalidOperationException("WebView2 smoke assertions failed.");
            foreach (var skin in new[] { "oak", "slate" })
            {
                await web.CoreWebView2.ExecuteScriptAsync($"document.body.dataset.skin='{skin}'");
                await Task.Delay(200);
                using var file = File.Create(Path.Combine(smoke!, skin + ".png"));
                await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
            }
            File.WriteAllText(Path.Combine(smoke!, "success.txt"), "Windows WebView2 initialized; pixel font loaded; three cards rendered; both skins captured.");
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(smoke!, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }
        finally { Close(); }
    }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hWnd, int message, int wParam, int lParam);
}
