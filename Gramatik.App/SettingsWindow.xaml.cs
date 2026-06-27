using System.Windows;
using System.Windows.Input;
using Gramatik.App.Models;
using Gramatik.App.Services;

namespace Gramatik.App;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly OpenRouterClient _openRouterClient;
    private readonly HotkeyService _hotkeyService;
    private readonly StartupService _startupService;
    private readonly AppLogger _logger;
    private readonly Action<string> _statusSink;
    private RuntimeSettings _workingSettings = new();
    private CaptureTarget? _captureTarget;
    private bool _isUpdatingTemperature;

    public SettingsWindow(
        SettingsService settingsService,
        OpenRouterClient openRouterClient,
        HotkeyService hotkeyService,
        StartupService startupService,
        AppLogger logger,
        Action<string> statusSink)
    {
        InitializeComponent();

        _settingsService = settingsService;
        _openRouterClient = openRouterClient;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _logger = logger;
        _statusSink = statusSink;

        PreviewKeyDown += SettingsWindow_PreviewKeyDown;
        PreviewMouseDown += SettingsWindow_PreviewMouseDown;

        LoadFromSettings(_settingsService.Current);
    }

    public void LoadFromSettings(RuntimeSettings settings)
    {
        _workingSettings = Clone(settings);
        ApiKeyBox.Password = _workingSettings.ApiKey;
        SetTemperatureControls(_workingSettings.Temperature);
        StartWithWindowsCheckBox.IsChecked = _workingSettings.StartWithWindows;
        RefreshModelList();
        RefreshHotkeyText();
        ReloadLogs();
        SetStatus("Ready.");
    }

    private async void RefreshModels_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync(saveAfterRefresh: false);
    }

    public async Task RefreshModelsAsync(bool saveAfterRefresh)
    {
        try
        {
            SetStatus("Refreshing models...");
            _logger.Info("SettingsRefreshModelsStarted", $"saveAfterRefresh={saveAfterRefresh}");
            var models = await _openRouterClient.GetModelsAsync(ApiKeyBox.Password);
            _workingSettings.CachedModels = models.ToList();

            if (_workingSettings.SelectedModelId is null
                || _workingSettings.CachedModels.All(model => model.Id != _workingSettings.SelectedModelId))
            {
                _workingSettings.SelectedModelId = _workingSettings.CachedModels.FirstOrDefault()?.Id;
            }

            RefreshModelList();

            if (saveAfterRefresh)
            {
                await SaveWorkingSettingsAsync();
            }

            SetStatus(models.Count == 0 ? "No text models returned by OpenRouter." : $"Loaded {models.Count} models.");
            _logger.Info("SettingsRefreshModelsFinished", $"models={models.Count}; saveAfterRefresh={saveAfterRefresh}");
        }
        catch (Exception ex)
        {
            _logger.Error("SettingsRefreshModelsFailed", ex);
            SetStatus("Could not refresh models. Check API key and network.");
        }
    }

    private void RecordCorrect_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(CaptureTarget.Correct);
    }

    private void RecordTranslate_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(CaptureTarget.Translate);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await SaveWorkingSettingsAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _logger.Info("SettingsWindowHidden");
        Hide();
    }

    private void ReloadLogs_Click(object sender, RoutedEventArgs e)
    {
        ReloadLogs();
        SetStatus("Logs reloaded.");
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        _logger.OpenLogDirectory();
        SetStatus($"Logs folder opened: {_logger.LogDirectory}");
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _logger.Clear();
        _logger.Info("LogsClearedFromSettings");
        ReloadLogs();
        SetStatus("Logs cleared.");
    }

    private void TemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingTemperature)
        {
            return;
        }

        SetTemperatureControls(e.NewValue);
    }

    private void TemperatureTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_isUpdatingTemperature)
        {
            return;
        }

        if (double.TryParse(TemperatureTextBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature))
        {
            SetTemperatureControls(temperature);
        }
    }

    private async Task SaveWorkingSettingsAsync()
    {
        _workingSettings.ApiKey = ApiKeyBox.Password;
        _workingSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _workingSettings.SelectedModelId = (ModelComboBox.SelectedItem as OpenRouterModel)?.Id ?? _workingSettings.SelectedModelId;
        _workingSettings.Temperature = ReadTemperature();

        var validation = SettingsService.Validate(_workingSettings);
        if (validation is not null)
        {
            _logger.Warning("SettingsValidationFailed", validation);
            SetStatus(validation);
            return;
        }

        try
        {
            await _settingsService.SaveAsync(Clone(_workingSettings));
            _startupService.SetStartWithWindows(_workingSettings.StartWithWindows);
            _hotkeyService.UpdateSettings(_settingsService.Current);
            SetStatus("Settings saved.");
            _statusSink("Settings saved.");
            _logger.Info("SettingsWindowSaveFinished");
        }
        catch (Exception ex)
        {
            _logger.Error("SettingsWindowSaveFailed", ex);
            SetStatus("Could not save settings.");
        }
    }

    private void BeginCapture(CaptureTarget target)
    {
        _captureTarget = target;
        _logger.Info("HotkeyRecordingStarted", $"target={target}");
        SetStatus("Press a keyboard shortcut or Middle/XButton1/XButton2. Esc cancels.");
    }

    private void SettingsWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_captureTarget is null)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _captureTarget = null;
            _logger.Info("HotkeyRecordingCancelled");
            SetStatus("Recording cancelled.");
            return;
        }

        if (IsModifierKey(key))
        {
            return;
        }

        ApplyCapturedHotkey(HotkeyBinding.Keyboard(key, ConvertModifiers(Keyboard.Modifiers)));
    }

    private void SettingsWindow_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_captureTarget is null)
        {
            return;
        }

        var button = e.ChangedButton switch
        {
            MouseButton.Middle => MouseButtonName.Middle,
            MouseButton.XButton1 => MouseButtonName.XButton1,
            MouseButton.XButton2 => MouseButtonName.XButton2,
            _ => (MouseButtonName?)null
        };

        if (button is null)
        {
            return;
        }

        e.Handled = true;
        ApplyCapturedHotkey(HotkeyBinding.Mouse(button.Value, ConvertModifiers(Keyboard.Modifiers)));
    }

    private void ApplyCapturedHotkey(HotkeyBinding binding)
    {
        if (_captureTarget == CaptureTarget.Correct)
        {
            _workingSettings.CorrectHotkey = binding;
        }
        else
        {
            _workingSettings.CorrectAndTranslateHotkey = binding;
        }

        _captureTarget = null;
        RefreshHotkeyText();

        var validation = SettingsService.Validate(_workingSettings);
        _logger.Info("HotkeyRecorded", $"binding={binding.ToDisplayString()}; validation={validation ?? "<ok>"}");
        SetStatus(validation ?? $"Recorded {binding.ToDisplayString()}.");
    }

    private void ReloadLogs()
    {
        try
        {
            LogsTextBox.Text = _logger.ReadAll();
            LogsTextBox.ScrollToEnd();
        }
        catch (Exception ex)
        {
            LogsTextBox.Text = $"Could not read logs: {ex.Message}";
        }
    }

    private void RefreshModelList()
    {
        ModelComboBox.ItemsSource = null;
        ModelComboBox.ItemsSource = _workingSettings.CachedModels;

        if (_workingSettings.SelectedModelId is not null)
        {
            ModelComboBox.SelectedItem = _workingSettings.CachedModels.FirstOrDefault(model => model.Id == _workingSettings.SelectedModelId);
        }

        if (ModelComboBox.SelectedItem is null && _workingSettings.CachedModels.Count > 0)
        {
            ModelComboBox.SelectedIndex = 0;
        }
    }

    private void RefreshHotkeyText()
    {
        CorrectHotkeyText.Text = _workingSettings.CorrectHotkey.ToDisplayString();
        TranslateHotkeyText.Text = _workingSettings.CorrectAndTranslateHotkey.ToDisplayString();
    }

    private void SetTemperatureControls(double temperature)
    {
        var normalized = NormalizeTemperature(temperature);
        _workingSettings.Temperature = normalized;

        _isUpdatingTemperature = true;
        TemperatureSlider.Value = normalized;
        TemperatureTextBox.Text = normalized.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        _isUpdatingTemperature = false;
    }

    private double ReadTemperature()
    {
        if (double.TryParse(TemperatureTextBox.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature))
        {
            return NormalizeTemperature(temperature);
        }

        return NormalizeTemperature(TemperatureSlider.Value);
    }

    private static double NormalizeTemperature(double temperature)
    {
        if (double.IsNaN(temperature))
        {
            return 0.5;
        }

        return Math.Clamp(Math.Round(temperature, 2), 0, 2);
    }

    private void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    private static RuntimeSettings Clone(RuntimeSettings settings)
    {
        return new RuntimeSettings
        {
            ApiKey = settings.ApiKey,
            SelectedModelId = settings.SelectedModelId,
            Temperature = settings.Temperature,
            CorrectHotkey = new HotkeyBinding
            {
                Kind = settings.CorrectHotkey.Kind,
                Modifiers = settings.CorrectHotkey.Modifiers,
                Key = settings.CorrectHotkey.Key,
                MouseButton = settings.CorrectHotkey.MouseButton
            },
            CorrectAndTranslateHotkey = new HotkeyBinding
            {
                Kind = settings.CorrectAndTranslateHotkey.Kind,
                Modifiers = settings.CorrectAndTranslateHotkey.Modifiers,
                Key = settings.CorrectAndTranslateHotkey.Key,
                MouseButton = settings.CorrectAndTranslateHotkey.MouseButton
            },
            StartWithWindows = settings.StartWithWindows,
            CachedModels = settings.CachedModels
                .Select(model => new OpenRouterModel
                {
                    Id = model.Id,
                    Name = model.Name,
                    Description = model.Description,
                    ContextLength = model.ContextLength
                })
                .ToList()
        };
    }

    private static HotkeyModifier ConvertModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifier.None;

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifier.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifier.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifier.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifier.Windows;
        }

        return result;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl
            or Key.RightCtrl
            or Key.LeftAlt
            or Key.RightAlt
            or Key.LeftShift
            or Key.RightShift
            or Key.LWin
            or Key.RWin;
    }

    private enum CaptureTarget
    {
        Correct,
        Translate
    }
}
