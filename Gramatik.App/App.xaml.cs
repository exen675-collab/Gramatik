using System.IO;
using System.Net.Http;
using System.Windows;
using Gramatik.App.Services;

namespace Gramatik.App;

public partial class App : System.Windows.Application
{
    private AppLogger? _logger;
    private SettingsService? _settingsService;
    private OpenRouterClient? _openRouterClient;
    private HotkeyService? _hotkeyService;
    private StartupService? _startupService;
    private TrayService? _trayService;
    private CorrectionCoordinator? _correctionCoordinator;
    private SettingsWindow? _settingsWindow;
    private bool _isShuttingDown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _logger = new AppLogger();
        _logger.Info("ApplicationStarting", $"version={typeof(App).Assembly.GetName().Version}; processPath={Environment.ProcessPath}");
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                _logger?.Error("AppDomainUnhandledException", exception, $"isTerminating={args.IsTerminating}");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Error("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        _settingsService = new SettingsService(
            new DpapiSecretProtector(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gramatik", "settings.json"),
            _logger);
        await _settingsService.LoadAsync();

        _openRouterClient = new OpenRouterClient(new HttpClient { BaseAddress = new Uri("https://openrouter.ai/api/v1/"), Timeout = TimeSpan.FromSeconds(60) }, _logger);
        _hotkeyService = new HotkeyService(_logger);
        _startupService = new StartupService();
        _startupService.SetStartWithWindows(_settingsService.Current.StartWithWindows);
        _logger.Info("StartupRegistrationApplied", $"enabled={_settingsService.Current.StartWithWindows}");

        var clipboardReplacementService = new ClipboardReplacementService(Dispatcher, _logger);
        _correctionCoordinator = new CorrectionCoordinator(_settingsService, _openRouterClient, clipboardReplacementService, _logger);

        _trayService = new TrayService();
        _trayService.SettingsRequested += (_, _) => ShowSettingsWindow();
        _trayService.RefreshModelsRequested += async (_, _) => await RefreshModelsFromTrayAsync();
        _trayService.OpenLogsRequested += (_, _) => _logger.OpenLogDirectory();
        _trayService.ExitRequested += (_, _) =>
        {
            _logger.Info("ExitRequested");
            _isShuttingDown = true;
            Shutdown();
        };

        _correctionCoordinator.StatusChanged += (_, status) => _trayService.SetStatus(status);
        _hotkeyService.HotkeyPressed += (_, mode) => Dispatcher.InvokeAsync(async () => await _correctionCoordinator.RunAsync(mode));
        _hotkeyService.Start(_settingsService.Current);
        _logger.Info("ApplicationStarted", $"logPath={_logger.LogPath}");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isShuttingDown = true;
        _settingsWindow?.Close();
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        _logger?.Info("ApplicationExited");
        base.OnExit(e);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsService is null || _openRouterClient is null || _hotkeyService is null || _startupService is null || _trayService is null)
        {
            _logger?.Warning("ShowSettingsSkipped", "servicesNotReady=true");
            return;
        }

        if (_settingsWindow is null)
        {
            _logger?.Info("SettingsWindowCreating");
            _settingsWindow = new SettingsWindow(
                _settingsService,
                _openRouterClient,
                _hotkeyService,
                _startupService,
                _logger!,
                status => _trayService.SetStatus(status));

            _settingsWindow.Closing += (_, args) =>
            {
                if (!_isShuttingDown)
                {
                    args.Cancel = true;
                    _settingsWindow.Hide();
                }
            };
        }
        else
        {
            _logger?.Info("SettingsWindowReloading");
            _settingsWindow.LoadFromSettings(_settingsService.Current);
        }

        _settingsWindow.Show();
        _settingsWindow.Activate();
        _logger?.Info("SettingsWindowShown");
    }

    private async Task RefreshModelsFromTrayAsync()
    {
        if (_settingsService is null || _openRouterClient is null || _trayService is null)
        {
            return;
        }

        try
        {
            _trayService.SetStatus("Refreshing models...");
            var settings = _settingsService.Current;
            _logger?.Info("TrayRefreshModelsStarted", $"hasApiKey={!string.IsNullOrWhiteSpace(settings.ApiKey)}");
            var models = await _openRouterClient.GetModelsAsync(settings.ApiKey);
            settings.CachedModels = models.ToList();

            if (settings.SelectedModelId is null || settings.CachedModels.All(model => model.Id != settings.SelectedModelId))
            {
                settings.SelectedModelId = settings.CachedModels.FirstOrDefault()?.Id;
            }

            await _settingsService.SaveAsync(settings);
            _settingsWindow?.LoadFromSettings(_settingsService.Current);
            _trayService.SetStatus(models.Count == 0 ? "No models loaded." : $"Loaded {models.Count} models.");
            _logger?.Info("TrayRefreshModelsFinished", $"models={models.Count}");
        }
        catch (Exception ex)
        {
            _logger?.Error("TrayRefreshModelsFailed", ex);
            _trayService.SetStatus("Model refresh failed.");
        }
    }
}
