namespace FeishuPixelTasks;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var smoke = args.Length == 2 && args[0] == "--smoke-test" ? Path.GetFullPath(args[1]) : null;
        using var mutex = new Mutex(true, smoke is null ? @"Local\FeishuPixelTasks-v1" : "FeishuPixelTasks-" + Guid.NewGuid(), out var first);
        if (!first) { MessageBox.Show("飞书像素任务已在运行，请在任务栏中切换窗口。"); return; }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try { Application.Run(new MainForm(smoke)); }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            if (smoke is not null) { Directory.CreateDirectory(smoke); File.WriteAllText(Path.Combine(smoke, "error.txt"), ex.ToString()); }
            else MessageBox.Show("启动失败。请保留数据目录，并确认已安装 Microsoft Edge WebView2 Runtime。\n\n" + ex.Message, "飞书像素任务");
        }
        mutex.ReleaseMutex();
    }
}
