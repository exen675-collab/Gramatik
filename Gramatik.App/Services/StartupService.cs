using Microsoft.Win32;

namespace Gramatik.App.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Gramatik";

    public void SetStartWithWindows(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
