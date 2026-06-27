namespace Gramatik.App.Models;

public sealed class RuntimeSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string? SelectedModelId { get; set; }

    public double Temperature { get; set; } = 0.5;

    public HotkeyBinding CorrectHotkey { get; set; } = HotkeyBinding.Keyboard(System.Windows.Input.Key.G, HotkeyModifier.Control | HotkeyModifier.Alt);

    public HotkeyBinding CorrectAndTranslateHotkey { get; set; } = HotkeyBinding.Keyboard(System.Windows.Input.Key.E, HotkeyModifier.Control | HotkeyModifier.Alt);

    public bool StartWithWindows { get; set; }

    public List<OpenRouterModel> CachedModels { get; set; } = [];

    public AppSettings ToStoredSettings(ISecretProtector protector)
    {
        return new AppSettings
        {
            EncryptedApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : protector.Protect(ApiKey),
            SelectedModelId = SelectedModelId,
            Temperature = Temperature,
            CorrectHotkey = CorrectHotkey,
            CorrectAndTranslateHotkey = CorrectAndTranslateHotkey,
            StartWithWindows = StartWithWindows,
            CachedModels = CachedModels
        };
    }
}
