using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gramatik.App.Models;

namespace Gramatik.App.Services;

public sealed class SettingsService
{
    private readonly ISecretProtector _protector;
    private readonly AppLogger? _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public SettingsService(ISecretProtector protector)
        : this(protector, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Gramatik", "settings.json"))
    {
    }

    public SettingsService(ISecretProtector protector, string settingsPath, AppLogger? logger = null)
    {
        _protector = protector;
        _logger = logger;
        SettingsPath = settingsPath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
    }

    public string SettingsPath { get; }

    public RuntimeSettings Current { get; private set; } = new();

    public async Task<RuntimeSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            Current = new RuntimeSettings();
            _logger?.Info("SettingsLoadDefault", $"path={SettingsPath}");
            return Current;
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var stored = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions, cancellationToken) ?? new AppSettings();

            Current = new RuntimeSettings
            {
                ApiKey = UnprotectOrEmpty(stored.EncryptedApiKey),
                SelectedModelId = stored.SelectedModelId,
                Temperature = NormalizeTemperature(stored.Temperature),
                CorrectHotkey = stored.CorrectHotkey,
                CorrectAndTranslateHotkey = stored.CorrectAndTranslateHotkey,
                StartWithWindows = stored.StartWithWindows,
                CachedModels = stored.CachedModels
            };

            _logger?.Info(
                "SettingsLoaded",
                $"path={SettingsPath}; hasApiKey={!string.IsNullOrWhiteSpace(Current.ApiKey)}; model={Current.SelectedModelId ?? "<none>"}; temperature={Current.Temperature:0.##}; models={Current.CachedModels.Count}; correctHotkey={Current.CorrectHotkey.ToDisplayString()}; translateHotkey={Current.CorrectAndTranslateHotkey.ToDisplayString()}; startup={Current.StartWithWindows}");
        }
        catch (Exception ex)
        {
            _logger?.Error("SettingsLoadFailed", ex, $"path={SettingsPath}");
            Current = new RuntimeSettings();
        }

        return Current;
    }

    public async Task SaveAsync(RuntimeSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings.ToStoredSettings(_protector), _jsonOptions, cancellationToken);
        Current = settings;
        _logger?.Info(
            "SettingsSaved",
            $"path={SettingsPath}; hasApiKey={!string.IsNullOrWhiteSpace(settings.ApiKey)}; model={settings.SelectedModelId ?? "<none>"}; temperature={settings.Temperature:0.##}; models={settings.CachedModels.Count}; correctHotkey={settings.CorrectHotkey.ToDisplayString()}; translateHotkey={settings.CorrectAndTranslateHotkey.ToDisplayString()}; startup={settings.StartWithWindows}");
    }

    public static string? Validate(RuntimeSettings settings)
    {
        if (!settings.CorrectHotkey.IsUsable())
        {
            return "Correct hotkey is not usable.";
        }

        if (!settings.CorrectAndTranslateHotkey.IsUsable())
        {
            return "Translate hotkey is not usable.";
        }

        if (settings.CorrectHotkey.Equals(settings.CorrectAndTranslateHotkey))
        {
            return "Both actions cannot use the same hotkey.";
        }

        if (settings.Temperature is < 0 or > 2 || double.IsNaN(settings.Temperature))
        {
            return "Temperature must be between 0.0 and 2.0.";
        }

        return null;
    }

    private static double NormalizeTemperature(double value)
    {
        if (value is < 0 or > 2 || double.IsNaN(value))
        {
            return 0.5;
        }

        return value;
    }

    private string UnprotectOrEmpty(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
        {
            return string.Empty;
        }

        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch
        {
            _logger?.Warning("ApiKeyUnprotectFailed");
            return string.Empty;
        }
    }
}
