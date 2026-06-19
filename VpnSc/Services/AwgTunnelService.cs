using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using VpnSc.Helpers;

namespace VpnSc.Services;

public static class AwgTunnelService
{
    private const string TunnelName = "vpnsc_awg";
    private static string? _configPath;

    public static bool IsConnected { get; private set; }

    public static string GetHelperPath() =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "awg_tunnel_service.exe");

    public static async Task<(bool ok, string? error)> ConnectAsync(string confIni)
    {
        if (!OsHelper.IsWindows10OrGreater())
            return (false, "AWG requires Windows 10 or newer");

        await DisconnectAsync();

        var helper = GetHelperPath();
        if (!File.Exists(helper))
            return (false, "awg_tunnel_service.exe not found next to vpn-sc.exe");

        if (!WriteConfigFile(confIni, TunnelName, out var configPath, out var writeErr))
            return (false, writeErr);

        _configPath = configPath;
        var statusPath = StatusFilePath();
        DeleteIfExists(statusPath);

        var args = "run \"" + configPath + "\" " + TunnelName + " \"" + statusPath + "\"";
        (bool ok, string? err) result;
        if (!IsAdmin())
            result = RunHelperElevated(args, statusPath);
        else
            result = await StartHelperProcessAsync(helper, args, statusPath);

        if (!result.ok)
        {
            IsConnected = false;
            return (false, result.err);
        }

        IsConnected = true;
        return (true, null);
    }

    public static async Task<(bool ok, string? error)> DisconnectAsync()
    {
        var helper = GetHelperPath();
        if (File.Exists(helper))
        {
            var statusPath = StatusFilePath();
            var args = "stop " + TunnelName + " \"" + statusPath + "\"";
            DeleteIfExists(statusPath);
            if (IsAdmin())
                await StartHelperProcessAsync(helper, args, statusPath);
            else
                RunHelperElevated(args, statusPath);
        }
        IsConnected = false;
        _configPath = null;
        return (true, null);
    }

    private static bool WriteConfigFile(string confIni, string tunnelName, out string path, out string? error)
    {
        error = null;
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrEmpty(programData))
            programData = @"C:\ProgramData";
        var dir = Path.Combine(programData, "awg_tunnel");
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, tunnelName + ".conf");
        try
        {
            File.WriteAllText(path, confIni, FileCompat.Utf8NoBom);
            return true;
        }
        catch (Exception ex)
        {
            error = "Cannot write config file: " + ex.Message;
            path = "";
            return false;
        }
    }

    private static string StatusFilePath()
    {
        var temp = Path.GetTempPath();
        var dir = Path.Combine(temp, "awg_tunnel");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "connect_status.txt");
    }

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static bool IsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static (bool ok, string? error) RunHelperElevated(string arguments, string statusPath)
    {
        DeleteIfExists(statusPath);
        var helper = GetHelperPath();
        var workDir = AppDomain.CurrentDomain.BaseDirectory;
        var result = (int)ShellExecute(IntPtr.Zero, "runas", helper, arguments, workDir, 0);
        if (result <= 32)
            return (false, "Administrator privileges required (UAC declined)");

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(statusPath))
                return ReadStatusFile(statusPath);
            Thread.Sleep(300);
        }
        return (false, "Timed out waiting for elevated tunnel install");
    }

    private static async Task<(bool ok, string? error)> StartHelperProcessAsync(string helper, string arguments, string statusPath)
    {
        DeleteIfExists(statusPath);
        var workDir = AppDomain.CurrentDomain.BaseDirectory;
        var psi = new ProcessStartInfo
        {
            FileName = helper,
            Arguments = arguments,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc == null)
            return (false, "CreateProcess failed");

        await Task.Run(() => proc.WaitForExit(120000));
        if (proc.ExitCode != 0)
        {
            if (File.Exists(statusPath))
                return ReadStatusFile(statusPath);
            return (false, "awg_tunnel_service.exe failed (exit " + proc.ExitCode + ")");
        }
        if (File.Exists(statusPath))
            return ReadStatusFile(statusPath);
        return (true, null);
    }

    private static (bool ok, string? error) ReadStatusFile(string path)
    {
        try
        {
            var content = File.ReadAllText(path).Trim();
            if (content == "ok") return (true, null);
            if (content.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
                return (false, content.Substring(6));
            return (false, string.IsNullOrEmpty(content) ? "Tunnel helper failed" : content);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);
}
