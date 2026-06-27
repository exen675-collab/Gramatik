using System.Windows;
using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.IDataObject;

namespace Gramatik.App.Services;

public sealed class ClipboardReplacementService
{
    private readonly Dispatcher _dispatcher;
    private readonly AppLogger? _logger;

    public ClipboardReplacementService(Dispatcher dispatcher, AppLogger? logger = null)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<bool> ReplaceSelectedTextAsync(
        Func<string, CancellationToken, Task<string?>> transformAsync,
        CancellationToken cancellationToken = default)
    {
        WpfDataObject? previousClipboard = null;
        var marker = $"GRAMATIK_EMPTY_SELECTION_{Guid.NewGuid():N}";

        try
        {
            _logger?.Info("ClipboardReplaceStarted");
            await WaitForModifierKeysReleasedAsync(cancellationToken);

            previousClipboard = await _dispatcher.InvokeAsync(() =>
            {
                var current = TryGetClipboardDataObject();
                WpfClipboard.SetText(marker);
                return current;
            });
            _logger?.Info("ClipboardPreviousCaptured", $"hasPrevious={previousClipboard is not null}");

            var copyInputsSent = NativeInput.SendCtrlShortcut(NativeInput.VK_C);
            _logger?.Info(
                "ClipboardCopyShortcutSent",
                $"inputsSent={copyInputsSent.SentInputCount}/{copyInputsSent.ExpectedInputCount}; inputSize={copyInputsSent.InputSize}; lastWin32Error={copyInputsSent.LastWin32Error}");
            await WaitForClipboardTextChangeAsync(marker, cancellationToken);

            var selectedText = await _dispatcher.InvokeAsync(() =>
                WpfClipboard.ContainsText() ? WpfClipboard.GetText() : string.Empty);
            _logger?.Info("ClipboardSelectionRead", $"hasText={!string.IsNullOrWhiteSpace(selectedText) && selectedText != marker}; length={(selectedText == marker ? 0 : selectedText.Length)}");

            if (string.IsNullOrWhiteSpace(selectedText) || selectedText == marker)
            {
                await RestoreClipboardAsync(previousClipboard);
                _logger?.Warning("ClipboardNoSelection");
                return false;
            }

            var replacement = await transformAsync(selectedText, cancellationToken);
            _logger?.Info("ClipboardTransformCompleted", $"hasReplacement={!string.IsNullOrWhiteSpace(replacement)}; replacementLength={replacement?.Length ?? 0}");
            if (string.IsNullOrWhiteSpace(replacement))
            {
                await RestoreClipboardAsync(previousClipboard);
                _logger?.Warning("ClipboardEmptyReplacement");
                return false;
            }

            await _dispatcher.InvokeAsync(() => WpfClipboard.SetText(replacement));
            await WaitForModifierKeysReleasedAsync(cancellationToken);
            var pasteInputsSent = NativeInput.SendCtrlShortcut(NativeInput.VK_V);
            _logger?.Info(
                "ClipboardPasteShortcutSent",
                $"inputsSent={pasteInputsSent.SentInputCount}/{pasteInputsSent.ExpectedInputCount}; inputSize={pasteInputsSent.InputSize}; lastWin32Error={pasteInputsSent.LastWin32Error}");
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            await RestoreClipboardAsync(previousClipboard);
            _logger?.Info("ClipboardReplaceFinished");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error("ClipboardReplaceFailed", ex);
            await RestoreClipboardAsync(previousClipboard);
            return false;
        }
    }

    private Task RestoreClipboardAsync(WpfDataObject? previousClipboard)
    {
        if (previousClipboard is null)
        {
            return Task.CompletedTask;
        }

        return _dispatcher.InvokeAsync(() =>
        {
            try
            {
                WpfClipboard.SetDataObject(previousClipboard, copy: true);
                _logger?.Info("ClipboardRestored");
            }
            catch (Exception ex)
            {
                _logger?.Error("ClipboardRestoreFailed", ex);
            }
        }).Task;
    }

    private async Task WaitForModifierKeysReleasedAsync(CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        while (NativeInput.IsAnyModifierKeyDown() && DateTimeOffset.UtcNow - start < TimeSpan.FromMilliseconds(1500))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        _logger?.Info("ModifierReleaseWaitFinished", $"modifiersStillDown={NativeInput.IsAnyModifierKeyDown()}; elapsedMs={(int)(DateTimeOffset.UtcNow - start).TotalMilliseconds}");
    }

    private async Task WaitForClipboardTextChangeAsync(string marker, CancellationToken cancellationToken)
    {
        var start = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - start < TimeSpan.FromMilliseconds(1200))
        {
            var changed = await _dispatcher.InvokeAsync(() =>
                WpfClipboard.ContainsText() && WpfClipboard.GetText() != marker);

            if (changed)
            {
                _logger?.Info("ClipboardCopyObserved", $"elapsedMs={(int)(DateTimeOffset.UtcNow - start).TotalMilliseconds}");
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        _logger?.Warning("ClipboardCopyTimeout", $"elapsedMs={(int)(DateTimeOffset.UtcNow - start).TotalMilliseconds}");
    }

    private static WpfDataObject? TryGetClipboardDataObject()
    {
        try
        {
            return WpfClipboard.GetDataObject();
        }
        catch (Exception ex)
        {
            _ = ex;
            return null;
        }
    }
}
