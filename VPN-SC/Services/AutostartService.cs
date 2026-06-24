using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace VpnSc.Services;

public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VpnSecurityConnect";
    private const string TaskName = "VPN-SC Autostart";
    private const string TaskDescription = "Start VPN Security Connect at user logon with elevated privileges.";

    private const int TaskCreateOrUpdate = 6; // TASK_CREATE_OR_UPDATE
    private const int TaskLogonInteractiveToken = 3; // TASK_LOGON_INTERACTIVE_TOKEN
    private const int TaskRunLevelHighest = 1; // TASK_RUNLEVEL_HIGHEST
    private const int TaskTriggerLogon = 9; // TASK_TRIGGER_LOGON
    private const int TaskActionExec = 0; // TASK_ACTION_EXEC

    public static bool IsEnabled()
    {
        if (IsScheduledTaskEnabled())
            return true;

        var legacyExe = GetLegacyRunExecutablePath();
        if (legacyExe is not { Length: > 0 })
            return false;

        // Best-effort migration from HKCU\Run to Task Scheduler.
        CreateOrUpdateScheduledTask(legacyExe);
        var scheduledEnabled = IsScheduledTaskEnabled();
        if (scheduledEnabled)
            DeleteLegacyRunValue();

        // If migration failed, legacy HKCU\Run still keeps autostart active.
        return true;
    }

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            DeleteScheduledTask();
            DeleteLegacyRunValue();
            return;
        }

        var exe = GetExecutablePath();
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            return;

        CreateOrUpdateScheduledTask(exe);
        if (IsScheduledTaskEnabled())
            DeleteLegacyRunValue();
    }

    private static string GetExecutablePath()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var path = process.MainModule?.FileName;
            if (path is { Length: > 0 })
                return path;
        }
        catch
        {
            /* ignore */
        }

        return System.Reflection.Assembly.GetExecutingAssembly().Location;
    }

    private static bool IsScheduledTaskEnabled()
    {
        object? service = null;
        object? rootFolder = null;
        object? task = null;
        try
        {
            (service, rootFolder) = ConnectScheduler();
            task = rootFolder!.GetType().InvokeMember(
                "GetTask",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                rootFolder,
                new object[] { TaskName });

            if (task == null)
                return false;

            var enabledObj = task.GetType().InvokeMember(
                "Enabled",
                System.Reflection.BindingFlags.GetProperty,
                null,
                task,
                null);

            return enabledObj is bool enabled && enabled;
        }
        catch (COMException)
        {
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            ReleaseCom(task);
            ReleaseCom(rootFolder);
            ReleaseCom(service);
        }
    }

    private static void CreateOrUpdateScheduledTask(string exePath)
    {
        object? service = null;
        object? rootFolder = null;
        object? taskDefinition = null;
        object? triggerCollection = null;
        object? actionCollection = null;
        object? trigger = null;
        object? action = null;
        try
        {
            (service, rootFolder) = ConnectScheduler();
            taskDefinition = service!.GetType().InvokeMember(
                "NewTask",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                service,
                new object[] { 0 });
            if (taskDefinition == null)
                return;

            var userId = GetCurrentUserId();

            SetNestedProperty(taskDefinition, new[] { "RegistrationInfo", "Description" }, TaskDescription);
            SetNestedProperty(taskDefinition, new[] { "Principal", "RunLevel" }, TaskRunLevelHighest);
            SetNestedProperty(taskDefinition, new[] { "Principal", "LogonType" }, TaskLogonInteractiveToken);
            SetNestedProperty(taskDefinition, new[] { "Principal", "UserId" }, userId);
            SetNestedProperty(taskDefinition, new[] { "Settings", "Enabled" }, true);
            SetNestedProperty(taskDefinition, new[] { "Settings", "AllowDemandStart" }, true);
            SetNestedProperty(taskDefinition, new[] { "Settings", "DisallowStartIfOnBatteries" }, false);
            SetNestedProperty(taskDefinition, new[] { "Settings", "StopIfGoingOnBatteries" }, false);
            SetNestedProperty(taskDefinition, new[] { "Settings", "StartWhenAvailable" }, true);

            triggerCollection = GetProperty(taskDefinition, "Triggers");
            if (triggerCollection != null)
            {
                trigger = triggerCollection.GetType().InvokeMember(
                    "Create",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    triggerCollection,
                    new object[] { TaskTriggerLogon });
                if (trigger != null)
                {
                    SetProperty(trigger, "Enabled", true);
                    SetProperty(trigger, "UserId", userId);
                }
            }

            actionCollection = GetProperty(taskDefinition, "Actions");
            if (actionCollection != null)
            {
                action = actionCollection.GetType().InvokeMember(
                    "Create",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    actionCollection,
                    new object[] { TaskActionExec });
                if (action != null)
                {
                    SetProperty(action, "Path", exePath);
                    SetProperty(action, "WorkingDirectory", Path.GetDirectoryName(exePath) ?? "");
                }
            }

            rootFolder!.GetType().InvokeMember(
                "RegisterTaskDefinition",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                rootFolder,
                new object[]
                {
                    TaskName,
                    taskDefinition,
                    TaskCreateOrUpdate,
                    userId,
                    null,
                    TaskLogonInteractiveToken,
                    null
                });
        }
        catch
        {
            /* ignore */
        }
        finally
        {
            ReleaseCom(action);
            ReleaseCom(trigger);
            ReleaseCom(actionCollection);
            ReleaseCom(triggerCollection);
            ReleaseCom(taskDefinition);
            ReleaseCom(rootFolder);
            ReleaseCom(service);
        }
    }

    private static void DeleteScheduledTask()
    {
        object? service = null;
        object? rootFolder = null;
        try
        {
            (service, rootFolder) = ConnectScheduler();
            rootFolder!.GetType().InvokeMember(
                "DeleteTask",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                rootFolder,
                new object[] { TaskName, 0 });
        }
        catch (COMException)
        {
            /* ignore missing task */
        }
        catch
        {
            /* ignore */
        }
        finally
        {
            ReleaseCom(rootFolder);
            ReleaseCom(service);
        }
    }

    private static (object service, object rootFolder) ConnectScheduler()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service");
        if (type == null)
            throw new InvalidOperationException("Task Scheduler COM is unavailable.");

        var service = Activator.CreateInstance(type);
        if (service == null)
            throw new InvalidOperationException("Failed to create Schedule.Service instance.");

        type.InvokeMember(
            "Connect",
            System.Reflection.BindingFlags.InvokeMethod,
            null,
            service,
            null);

        var rootFolder = type.InvokeMember(
            "GetFolder",
            System.Reflection.BindingFlags.InvokeMethod,
            null,
            service,
            new object[] { "\\" });

        if (rootFolder == null)
            throw new InvalidOperationException("Task Scheduler root folder is unavailable.");

        return (service, rootFolder);
    }

    private static void DeleteLegacyRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            key?.DeleteValue(ValueName, false);
        }
        catch
        {
            /* ignore */
        }
    }

    private static string? GetLegacyRunExecutablePath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
                return null;

            var path = value.Trim().Trim('"');
            return File.Exists(path) ? path : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetCurrentUserId()
    {
        try
        {
            return WindowsIdentity.GetCurrent().Name;
        }
        catch
        {
            var domain = Environment.UserDomainName;
            var user = Environment.UserName;
            if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(user))
                return domain + "\\" + user;
            return user;
        }
    }

    private static object? GetProperty(object source, string propertyName)
    {
        return source.GetType().InvokeMember(
            propertyName,
            System.Reflection.BindingFlags.GetProperty,
            null,
            source,
            null);
    }

    private static void SetProperty(object source, string propertyName, object value)
    {
        source.GetType().InvokeMember(
            propertyName,
            System.Reflection.BindingFlags.SetProperty,
            null,
            source,
            new[] { value });
    }

    private static void SetNestedProperty(object source, IReadOnlyList<string> chain, object value)
    {
        if (chain.Count == 0)
            return;

        object? current = source;
        for (var i = 0; i < chain.Count - 1; i++)
        {
            if (current == null)
                return;
            current = GetProperty(current, chain[i]);
        }

        if (current == null)
            return;
        SetProperty(current, chain[chain.Count - 1], value);
    }

    private static void ReleaseCom(object? comObject)
    {
        try
        {
            if (comObject != null && Marshal.IsComObject(comObject))
                Marshal.FinalReleaseComObject(comObject);
        }
        catch
        {
            /* ignore */
        }
    }
}

