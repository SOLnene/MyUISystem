#if UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class CloudSaveServerLauncher
{
    const string MenuRoot = "Tools/Cloud Save Server/";
    static readonly HttpClient healthClient = new HttpClient(new HttpClientHandler { UseProxy = false })
        { Timeout = TimeSpan.FromSeconds(2) };
    static bool starting;
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string ServerDirectory => Path.Combine(ProjectRoot, "Tools", "CloudSaveServer");
    static string Executable => Path.Combine(ServerDirectory, "out", "CloudSaveServer.exe");
    static string PreferenceKey => "MyUISystem.CloudServer." + Application.dataPath;

    internal static bool AutoStart
    {
        get => EditorPrefs.GetBool(PreferenceKey + ".AutoStart", false);
        set => EditorPrefs.SetBool(PreferenceKey + ".AutoStart", value);
    }

    [MenuItem(MenuRoot + "Start")]
    static void StartMenu() => StartAsync(CancellationToken.None).Forget(Debug.LogException);

    [MenuItem(MenuRoot + "Auto Start On Cloud Login")]
    static void ToggleAutoStart() => AutoStart = !AutoStart;

    [MenuItem(MenuRoot + "Auto Start On Cloud Login", true)]
    static bool ValidateAutoStart()
    {
        Menu.SetChecked(MenuRoot + "Auto Start On Cloud Login", AutoStart);
        return true;
    }

    [MenuItem(MenuRoot + "Status")]
    static void StatusMenu() => ShowStatusAsync().Forget(Debug.LogException);

    static async UniTask ShowStatusAsync()
    {
        bool available = await IsHealthyAsync(CancellationToken.None);
        Debug.Log(available ? "本机存档服务器正在运行。" : "本机存档服务器尚未就绪。");
    }

    [MenuItem(MenuRoot + "Stop Owned Server")]
    static void StopMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("请先停止游戏播放，再停止工具启动的服务器。");
            return;
        }
        using var process = GetOwnedProcess();
        if (process == null)
        {
            Debug.Log("没有工具启动的服务器；手动启动的服务由原窗口停止。");
            return;
        }
        process.Kill();
        EditorPrefs.DeleteKey(PreferenceKey + ".Pid");
        EditorPrefs.DeleteKey(PreferenceKey + ".Started");
        Debug.Log("已停止工具启动的本机存档服务器。");
    }

    internal static async UniTask EnsureRunningAsync(CancellationToken cancellationToken)
    {
        if (AutoStart && new Uri(CloudSaveHttpClient.ServiceUrl).IsLoopback)
            await StartAsync(cancellationToken);
    }

    static async UniTask StartAsync(CancellationToken cancellationToken)
    {
        while (starting)
            await Task.Delay(100, cancellationToken).AsUniTask();
        cancellationToken.ThrowIfCancellationRequested();
        starting = true;
        try
        {
            if (await IsHealthyAsync(cancellationToken))
                return;
            if (!File.Exists(Executable))
                throw new FileNotFoundException("服务器尚未发布，请先发布 Tools/CloudSaveServer。", Executable);
            using var existing = GetOwnedProcess();
            if (existing == null)
            {
                var info = new ProcessStartInfo(Executable)
                {
                    WorkingDirectory = ServerDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = new Process { StartInfo = info };
                process.Start();
                // PID 会复用，停止时还要核对启动时间和程序路径。
                EditorPrefs.SetInt(PreferenceKey + ".Pid", process.Id);
                EditorPrefs.SetString(PreferenceKey + ".Started", process.StartTime.ToUniversalTime().Ticks.ToString());
                string logs = Path.Combine(ProjectRoot, ".codex_tmp_frame_analysis", "cloud-save-editor.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logs));
                // 不重定向子进程输出，避免 Unity 域重载关闭管道而影响独立服务器。
                File.AppendAllText(logs, DateTime.UtcNow.ToString("O") + " started PID " + process.Id + Environment.NewLine);
                Debug.Log("已启动本机存档服务器，启动记录位于 .codex_tmp_frame_analysis/cloud-save-editor.log。");
            }
            var deadline = DateTime.UtcNow.AddSeconds(10);
            do
            {
                if (await IsHealthyAsync(cancellationToken))
                    return;
                await Task.Delay(150, cancellationToken).AsUniTask();
            } while (DateTime.UtcNow < deadline);
            throw new InvalidOperationException("服务器启动后未就绪，请检查端口占用和服务器日志。");
        }
        finally { starting = false; }
    }

    static Process GetOwnedProcess()
    {
        int id = EditorPrefs.GetInt(PreferenceKey + ".Pid", 0);
        if (id == 0) return null;
        Process process = null;
        try
        {
            process = Process.GetProcessById(id);
            if (!process.HasExited &&
                process.StartTime.ToUniversalTime().Ticks.ToString() == EditorPrefs.GetString(PreferenceKey + ".Started") &&
                string.Equals(process.MainModule.FileName, Executable, StringComparison.OrdinalIgnoreCase))
                return process;
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException ||
            error is System.ComponentModel.Win32Exception) { }
        process?.Dispose();
        return null;
    }

    static async UniTask<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await healthClient.GetAsync(
                new Uri(new Uri(CloudSaveHttpClient.ServiceUrl), "/health"), cancellationToken).AsUniTask();
            string body = await response.Content.ReadAsStringAsync().AsUniTask();
            return response.IsSuccessStatusCode && body.Contains("\"status\":\"ok\"");
        }
        catch (HttpRequestException) { return false; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }
}
#endif
