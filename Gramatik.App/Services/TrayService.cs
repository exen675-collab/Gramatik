using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Gramatik.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayService()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var refreshModelsItem = new ToolStripMenuItem("Refresh models");
        refreshModelsItem.Click += (_, _) => RefreshModelsRequested?.Invoke(this, EventArgs.Empty);

        var openLogsItem = new ToolStripMenuItem("Open logs folder");
        openLogsItem.Click += (_, _) => OpenLogsRequested?.Invoke(this, EventArgs.Empty);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(settingsItem);
        menu.Items.Add(refreshModelsItem);
        menu.Items.Add(openLogsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadApplicationIcon(),
            Text = "Gramatik - Ready",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? RefreshModelsRequested;

    public event EventHandler? OpenLogsRequested;

    public event EventHandler? ExitRequested;

    public void SetStatus(string status)
    {
        var text = $"Gramatik - {status}";
        _notifyIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _disposed = true;
    }

    private static Icon LoadApplicationIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "gramatik.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location) ?? SystemIcons.Application;
    }
}
