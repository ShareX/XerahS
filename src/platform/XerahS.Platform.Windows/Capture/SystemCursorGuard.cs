using System.Runtime.InteropServices;
using XerahS.Common;

namespace XerahS.Platform.Windows.Capture;

/// <summary>
/// Temporarily replaces every Windows system cursor with a transparent one so a capture does not
/// include a software-rendered pointer. SetSystemCursor changes the cursor for the whole desktop,
/// so a crash between hide and restore would leave the user without a visible mouse pointer
/// (XerahS issue #288). The guard restores on process exit and unhandled exceptions, and a marker
/// file lets the next launch restore the cursor after a hard crash that skipped both.
/// </summary>
internal static class SystemCursorGuard
{
    private const uint SPI_SETCURSORS = 0x0057;

    private static readonly uint[] AllCursorIds =
    {
        32512, // IDC_ARROW
        32513, // IDC_IBEAM
        32514, // IDC_WAIT
        32515, // IDC_CROSS
        32516, // IDC_UPARROW
        32642, // IDC_SIZENWSE
        32643, // IDC_SIZENESW
        32644, // IDC_SIZEWE
        32645, // IDC_SIZENS
        32646, // IDC_SIZEALL
        32648, // IDC_NO
        32649, // IDC_HAND
        32650, // IDC_APPSTARTING
    };

    private static readonly object _lock = new();
    private static int _hideCount;
    private static bool _exitHandlersRegistered;

    private static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XerahS",
        "system-cursors-hidden.marker");

    /// <summary>Restores system cursors left hidden by a previous XerahS process that crashed mid-capture.</summary>
    public static void RecoverFromPreviousSession()
    {
        try
        {
            if (!File.Exists(MarkerPath))
            {
                return;
            }

            DebugHelper.WriteLine("System cursors were left hidden by a previous session; restoring them.");
            TryRestoreAndClearMarker(MarkerPath, ReloadSystemCursors);
        }
        catch (Exception ex)
        {
            DebugHelper.WriteException(ex, "Failed to recover system cursors from a previous session");
        }
    }

    /// <summary>Hides all system cursors. Every successful call must be paired with <see cref="Restore"/>.</summary>
    public static bool TryHide()
    {
        lock (_lock)
        {
            if (_hideCount > 0)
            {
                _hideCount++;
                return true;
            }

            RegisterExitHandlers();

            try
            {
                // Write the marker first: if the process dies after SetSystemCursor, the next launch still finds it.
                Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath)!);
                File.WriteAllText(MarkerPath, Environment.ProcessId.ToString());
            }
            catch (Exception ex)
            {
                // Without a marker a hard crash could not be recovered on the next launch; capture with the cursor instead.
                DebugHelper.WriteLine($"SystemCursorGuard: cannot write marker, leaving cursors visible. {ex.Message}");
                return false;
            }

            if (!ReplaceWithBlankCursors())
            {
                TryRestoreAndClearMarker(MarkerPath, ReloadSystemCursors);
                return false;
            }

            _hideCount = 1;
            return true;
        }
    }

    public static void Restore()
    {
        lock (_lock)
        {
            if (_hideCount == 0)
            {
                return;
            }

            _hideCount--;
            if (_hideCount == 0)
            {
                TryRestoreAndClearMarker(MarkerPath, ReloadSystemCursors);
            }
        }
    }

    private static void RestoreOnExit()
    {
        lock (_lock)
        {
            if (_hideCount == 0 && !File.Exists(MarkerPath)) return;

            _hideCount = 0;
            TryRestoreAndClearMarker(MarkerPath, ReloadSystemCursors);
        }
    }

    private static void RegisterExitHandlers()
    {
        if (_exitHandlersRegistered)
        {
            return;
        }

        _exitHandlersRegistered = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreOnExit();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => RestoreOnExit();
    }

    private static bool ReplaceWithBlankCursors()
    {
        try
        {
            // 32x32 fully transparent cursor: AND mask all 1s keeps the screen, XOR mask all 0s adds nothing.
            var andMask = new byte[128];
            var xorMask = new byte[128];
            Array.Fill(andMask, (byte)0xFF);

            IntPtr blankCursor = CreateCursor(IntPtr.Zero, 0, 0, 32, 32, andMask, xorMask);
            if (blankCursor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                bool replacedAny = CursorReplacementHelper.TryReplaceSystemCursors(
                    AllCursorIds,
                    () => CopyIcon(blankCursor),
                    (copy, id) => SetSystemCursor(copy, id),
                    copy => DestroyCursor(copy));

                return replacedAny;
            }
            finally
            {
                DestroyCursor(blankCursor);
            }
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"SystemCursorGuard: failed to hide cursors. {ex.Message}");
            // A partial replacement must not be left behind.
            ReloadSystemCursors();
            return false;
        }
    }

    private static bool ReloadSystemCursors()
    {
        try
        {
            if (SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0)) return true;
            DebugHelper.WriteLine($"SystemCursorGuard: cursor restore failed (Win32 error {Marshal.GetLastWin32Error()}); keeping recovery marker.");
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"SystemCursorGuard: failed to restore cursors. {ex.Message}");
        }
        return false;
    }

    // A failed native restore must remain recoverable at exit or the next launch.
    internal static bool TryRestoreAndClearMarker(string markerPath, Func<bool> reload)
    {
        try
        {
            if (!reload()) return false;
            File.Delete(markerPath);
            return true;
        }
        catch (Exception ex)
        {
            DebugHelper.WriteLine($"SystemCursorGuard: keeping recovery marker. {ex.Message}");
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetSystemCursor(IntPtr hcur, uint id);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot,
        int nWidth, int nHeight, byte[] pvANDPlane, byte[] pvXORPlane);

    [DllImport("user32.dll")]
    private static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
}
