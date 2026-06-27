using System.Runtime.InteropServices;

namespace Gramatik.App.Services;

internal static class NativeInput
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int VK_CONTROL = 0x11;
    public const int VK_C = 0x43;
    public const int VK_V = 0x56;
    public const int VK_MENU = 0x12;
    public const int VK_SHIFT = 0x10;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int INPUT_KEYBOARD = 1;
    public const int KEYEVENTF_KEYUP = 0x0002;
    public const int XBUTTON1 = 0x0001;
    public const int XBUTTON2 = 0x0002;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    public static bool IsAnyModifierKeyDown()
    {
        return IsKeyDown(VK_CONTROL)
            || IsKeyDown(VK_MENU)
            || IsKeyDown(VK_SHIFT)
            || IsKeyDown(VK_LWIN)
            || IsKeyDown(VK_RWIN);
    }

    public static SendInputResult SendCtrlShortcut(int virtualKey)
    {
        var inputs = new[]
        {
            KeyInput(VK_CONTROL, keyUp: false),
            KeyInput(virtualKey, keyUp: false),
            KeyInput(virtualKey, keyUp: true),
            KeyInput(VK_CONTROL, keyUp: true)
        };

        var inputSize = Marshal.SizeOf<INPUT>();
        var sent = SendInput((uint)inputs.Length, inputs, inputSize);
        return new SendInputResult(inputs.Length, sent, inputSize, sent == inputs.Length ? 0 : Marshal.GetLastWin32Error());
    }

    private static INPUT KeyInput(int virtualKey, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = (ushort)virtualKey,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
                }
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;

        [FieldOffset(0)]
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public int dwFlags;
        public int time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    public readonly record struct SendInputResult(
        int ExpectedInputCount,
        uint SentInputCount,
        int InputSize,
        int LastWin32Error);
}
