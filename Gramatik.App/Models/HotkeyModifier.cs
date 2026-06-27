using System;

namespace Gramatik.App.Models;

[Flags]
public enum HotkeyModifier
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8
}
