using Gramatik.App.Models;

namespace Gramatik.App.Services;

public sealed class CorrectionCoordinator
{
    private readonly SettingsService _settingsService;
    private readonly OpenRouterClient _openRouterClient;
    private readonly ClipboardReplacementService _clipboardReplacementService;
    private readonly AppLogger? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CorrectionCoordinator(
        SettingsService settingsService,
        OpenRouterClient openRouterClient,
        ClipboardReplacementService clipboardReplacementService,
        AppLogger? logger = null)
    {
        _settingsService = settingsService;
        _openRouterClient = openRouterClient;
        _clipboardReplacementService = clipboardReplacementService;
        _logger = logger;
    }

    public event EventHandler<string>? StatusChanged;

    public string LastStatus { get; private set; } = "Ready.";

    public async Task RunAsync(CorrectionMode mode, CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            _logger?.Warning("CorrectionIgnoredBusy", $"mode={mode}");
            SetStatus("Busy. Ignored overlapping request.");
            return;
        }

        try
        {
            var settings = _settingsService.Current;
            _logger?.Info("CorrectionStarted", $"mode={mode}; hasApiKey={!string.IsNullOrWhiteSpace(settings.ApiKey)}; model={settings.SelectedModelId ?? "<none>"}; temperature={settings.Temperature:0.##}");

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                _logger?.Warning("CorrectionMissingApiKey", $"mode={mode}");
                SetStatus("Missing OpenRouter API key.");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.SelectedModelId))
            {
                _logger?.Warning("CorrectionMissingModel", $"mode={mode}");
                SetStatus("Missing selected OpenRouter model.");
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(65));

            SetStatus(mode == CorrectionMode.Correct ? "Correcting selection..." : "Correcting and translating selection...");
            var changed = await _clipboardReplacementService.ReplaceSelectedTextAsync(
                (text, token) => _openRouterClient.CorrectAsync(settings.ApiKey, settings.SelectedModelId, mode, text, settings.Temperature, token),
                timeout.Token);

            _logger?.Info("CorrectionFinished", $"mode={mode}; changed={changed}");
            SetStatus(changed ? "Selection replaced." : "No text changed.");
        }
        catch (Exception ex)
        {
            _logger?.Error("CorrectionFailed", ex, $"mode={mode}");
            SetStatus("Request failed. Check API key, model, and network.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void SetStatus(string status)
    {
        LastStatus = status;
        StatusChanged?.Invoke(this, status);
    }
}
