namespace Gramatik.App.Models;

public sealed class AppSettings
{
    public string? EncryptedApiKey { get; set; }

    public string? SelectedModelId { get; set; }

    public double Temperature { get; set; } = 0.5;

    public HotkeyBinding CorrectHotkey { get; set; } = HotkeyBinding.Keyboard(System.Windows.Input.Key.G, HotkeyModifier.Control | HotkeyModifier.Alt);

    public HotkeyBinding CorrectAndTranslateHotkey { get; set; } = HotkeyBinding.Keyboard(System.Windows.Input.Key.E, HotkeyModifier.Control | HotkeyModifier.Alt);

    public bool StartWithWindows { get; set; }

    public List<OpenRouterModel> CachedModels { get; set; } = [];
}
