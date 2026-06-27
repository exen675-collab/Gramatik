using Gramatik.App.Models;
using Gramatik.App.Services;
using System.Windows.Input;

namespace Gramatik.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task SaveAsync_DoesNotWritePlainApiKey()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gramatik-{Guid.NewGuid():N}.json");
        var protector = new ReversingProtector();
        var service = new SettingsService(protector, path);

        await service.SaveAsync(new RuntimeSettings
        {
            ApiKey = "sk-or-secret",
            SelectedModelId = "openai/gpt-test",
            Temperature = 0.7,
            CorrectHotkey = HotkeyBinding.Keyboard(Key.G, HotkeyModifier.Control),
            CorrectAndTranslateHotkey = HotkeyBinding.Mouse(MouseButtonName.XButton2)
        });

        var json = await File.ReadAllTextAsync(path);

        Assert.DoesNotContain("sk-or-secret", json);
        Assert.Contains("\"Temperature\": 0.7", json);
        Assert.Contains(Convert.ToBase64String("sk-or-secret".Reverse().Select(ch => (byte)ch).ToArray()), json);
    }

    [Fact]
    public void Validate_RejectsDuplicateHotkeys()
    {
        var binding = HotkeyBinding.Keyboard(Key.G, HotkeyModifier.Control | HotkeyModifier.Alt);
        var settings = new RuntimeSettings
        {
            CorrectHotkey = binding,
            CorrectAndTranslateHotkey = HotkeyBinding.Keyboard(Key.G, HotkeyModifier.Control | HotkeyModifier.Alt)
        };

        var error = SettingsService.Validate(settings);

        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_RejectsTemperatureOutOfRange()
    {
        var settings = new RuntimeSettings
        {
            Temperature = 2.5
        };

        var error = SettingsService.Validate(settings);

        Assert.NotNull(error);
    }

    private sealed class ReversingProtector : ISecretProtector
    {
        public string Protect(string value)
        {
            return Convert.ToBase64String(value.Reverse().Select(ch => (byte)ch).ToArray());
        }

        public string Unprotect(string protectedValue)
        {
            return new string(Convert.FromBase64String(protectedValue).Select(value => (char)value).Reverse().ToArray());
        }
    }
}
