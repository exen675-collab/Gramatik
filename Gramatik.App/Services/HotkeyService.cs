using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Gramatik.App.Models;

namespace Gramatik.App.Services;

public sealed class HotkeyService : IDisposable
{
    private readonly NativeInput.HookProc _keyboardProc;
    private readonly NativeInput.HookProc _mouseProc;
    private readonly AppLogger? _logger;
    private readonly Dictionary<CorrectionMode, DateTimeOffset> _lastTriggers = [];
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private RuntimeSettings _settings = new();
    private bool _disposed;

    public HotkeyService(AppLogger? logger = null)
    {
        _logger = logger;
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public event EventHandler<CorrectionMode>? HotkeyPressed;

    public void Start(RuntimeSettings settings)
    {
        UpdateSettings(settings);

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = NativeInput.GetModuleHandle(module?.ModuleName);

        _keyboardHook = NativeInput.SetWindowsHookEx(NativeInput.WH_KEYBOARD_LL, _keyboardProc, moduleHandle, 0);
        _mouseHook = NativeInput.SetWindowsHookEx(NativeInput.WH_MOUSE_LL, _mouseProc, moduleHandle, 0);
        _logger?.Info("HotkeysStarted", $"keyboardHook={_keyboardHook != IntPtr.Zero}; mouseHook={_mouseHook != IntPtr.Zero}; correct={settings.CorrectHotkey.ToDisplayString()}; translate={settings.CorrectAndTranslateHotkey.ToDisplayString()}");
    }

    public void UpdateSettings(RuntimeSettings settings)
    {
        _settings = settings;
        _logger?.Info("HotkeysUpdated", $"correct={settings.CorrectHotkey.ToDisplayString()}; translate={settings.CorrectAndTranslateHotkey.ToDisplayString()}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeInput.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            _logger?.Info("KeyboardHookStopped");
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeInput.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
            _logger?.Info("MouseHookStopped");
        }

        _disposed = true;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == NativeInput.WM_KEYDOWN || wParam == NativeInput.WM_SYSKEYDOWN))
        {
            var hook = Marshal.PtrToStructure<NativeInput.KBDLLHOOKSTRUCT>(lParam);
            var key = KeyInterop.KeyFromVirtualKey((int)hook.vkCode);

            if (!IsModifierKey(key))
            {
                var binding = HotkeyBinding.Keyboard(key, ReadCurrentModifiers());
                if (TryTrigger(binding))
                {
                    _logger?.Info("HotkeySuppressed", $"binding={binding.ToDisplayString()}");
                    return 1;
                }
            }
        }

        return NativeInput.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var button = ReadMouseButton(wParam, lParam);
            if (button is not null)
            {
                var binding = HotkeyBinding.Mouse(button.Value, ReadCurrentModifiers());
                if (TryTrigger(binding))
                {
                    _logger?.Info("HotkeySuppressed", $"binding={binding.ToDisplayString()}");
                    return 1;
                }
            }
        }

        return NativeInput.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private bool TryTrigger(HotkeyBinding binding)
    {
        if (binding.Equals(_settings.CorrectHotkey))
        {
            _logger?.Info("HotkeyMatched", $"mode={CorrectionMode.Correct}; binding={binding.ToDisplayString()}");
            RaiseThrottled(CorrectionMode.Correct);
            return true;
        }
        else if (binding.Equals(_settings.CorrectAndTranslateHotkey))
        {
            _logger?.Info("HotkeyMatched", $"mode={CorrectionMode.CorrectAndTranslateToEnglish}; binding={binding.ToDisplayString()}");
            RaiseThrottled(CorrectionMode.CorrectAndTranslateToEnglish);
            return true;
        }

        return false;
    }

    private void RaiseThrottled(CorrectionMode mode)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastTriggers.TryGetValue(mode, out var last) && now - last < TimeSpan.FromMilliseconds(700))
        {
            _logger?.Warning("HotkeyThrottled", $"mode={mode}");
            return;
        }

        _lastTriggers[mode] = now;
        _logger?.Info("HotkeyRaised", $"mode={mode}");
        HotkeyPressed?.Invoke(this, mode);
    }

    private static MouseButtonName? ReadMouseButton(IntPtr wParam, IntPtr lParam)
    {
        if (wParam == NativeInput.WM_MBUTTONDOWN)
        {
            return MouseButtonName.Middle;
        }

        if (wParam != NativeInput.WM_XBUTTONDOWN)
        {
            return null;
        }

        var hook = Marshal.PtrToStructure<NativeInput.MSLLHOOKSTRUCT>(lParam);
        var highWord = (hook.mouseData >> 16) & 0xffff;
        return highWord switch
        {
            NativeInput.XBUTTON1 => MouseButtonName.XButton1,
            NativeInput.XBUTTON2 => MouseButtonName.XButton2,
            _ => null
        };
    }

    public static HotkeyModifier ReadCurrentModifiers()
    {
        var modifiers = HotkeyModifier.None;

        if (NativeInput.IsKeyDown(NativeInput.VK_CONTROL))
        {
            modifiers |= HotkeyModifier.Control;
        }

        if (NativeInput.IsKeyDown(NativeInput.VK_MENU))
        {
            modifiers |= HotkeyModifier.Alt;
        }

        if (NativeInput.IsKeyDown(NativeInput.VK_SHIFT))
        {
            modifiers |= HotkeyModifier.Shift;
        }

        if (NativeInput.IsKeyDown(NativeInput.VK_LWIN) || NativeInput.IsKeyDown(NativeInput.VK_RWIN))
        {
            modifiers |= HotkeyModifier.Windows;
        }

        return modifiers;
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
}
