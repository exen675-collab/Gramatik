using System;
using System.Windows.Input;

namespace Gramatik.App.Models;

public sealed class HotkeyBinding : IEquatable<HotkeyBinding>
{
    public HotkeyKind Kind { get; set; }

    public HotkeyModifier Modifiers { get; set; }

    public Key Key { get; set; } = Key.None;

    public MouseButtonName MouseButton { get; set; } = MouseButtonName.XButton1;

    public static HotkeyBinding Keyboard(Key key, HotkeyModifier modifiers)
    {
        return new HotkeyBinding
        {
            Kind = HotkeyKind.Keyboard,
            Key = key,
            Modifiers = modifiers
        };
    }

    public static HotkeyBinding Mouse(MouseButtonName mouseButton, HotkeyModifier modifiers = HotkeyModifier.None)
    {
        return new HotkeyBinding
        {
            Kind = HotkeyKind.Mouse,
            MouseButton = mouseButton,
            Modifiers = modifiers
        };
    }

    public string ToDisplayString()
    {
        var prefix = Modifiers == HotkeyModifier.None ? string.Empty : $"{FormatModifiers(Modifiers)}+";
        return Kind == HotkeyKind.Keyboard
            ? $"{prefix}{Key}"
            : $"{prefix}{MouseButton}";
    }

    public bool IsUsable()
    {
        if (Kind == HotkeyKind.Keyboard)
        {
            return Key != Key.None
                && Key != Key.LeftCtrl
                && Key != Key.RightCtrl
                && Key != Key.LeftAlt
                && Key != Key.RightAlt
                && Key != Key.LeftShift
                && Key != Key.RightShift
                && Key != Key.LWin
                && Key != Key.RWin;
        }

        return MouseButton is MouseButtonName.Middle or MouseButtonName.XButton1 or MouseButtonName.XButton2;
    }

    public bool Equals(HotkeyBinding? other)
    {
        return other is not null
            && Kind == other.Kind
            && Modifiers == other.Modifiers
            && Key == other.Key
            && MouseButton == other.MouseButton;
    }

    public override bool Equals(object? obj) => Equals(obj as HotkeyBinding);

    public override int GetHashCode() => HashCode.Combine(Kind, Modifiers, Key, MouseButton);

    private static string FormatModifiers(HotkeyModifier modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(HotkeyModifier.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(HotkeyModifier.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(HotkeyModifier.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(HotkeyModifier.Windows))
        {
            parts.Add("Win");
        }

        return string.Join("+", parts);
    }
}
