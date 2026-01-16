#region License Information (GPL v3)

/*
    ShareX.Ava - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2025 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using Avalonia.Input;
using XerahS.Platform.Abstractions;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

// Only import DebugHelper from Common, not the whole namespace
using DebugHelper = XerahS.Common.DebugHelper;

namespace XerahS.Platform.Windows;

/// <summary>
/// Windows implementation of global hotkey registration using RegisterHotKey API
/// </summary>
public class WindowsHotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_USER = 0x0400;
    private const int WM_INVOKE = WM_USER + 1;

    private readonly Dictionary<ushort, HotkeyInfo> _registeredHotkeys = new();
    private readonly ConcurrentQueue<Action> _actionQueue = new();
    private readonly object _lock = new(); // Only for accessing _registeredHotkeys if needed from other threads
    private ushort _nextId = 1;
    private bool _disposed;
    private IntPtr _hwnd;
    private Thread? _messageThread;
    private bool _running;
    private int _messageThreadId;

    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;
    public bool IsSuspended { get; set; }

    #region P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(int idThread, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    // Modifier flags for RegisterHotKey
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private const uint WM_QUIT = 0x0012;

    #endregion

    public WindowsHotkeyService()
    {
        StartMessageLoop();
    }

    private void StartMessageLoop()
    {
        _running = true;
        _messageThread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "HotkeyMessageLoop"
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();

        // Wait for window to be created
        SpinWait.SpinUntil(() => _hwnd != IntPtr.Zero, TimeSpan.FromSeconds(5));
    }

    private void MessageLoop()
    {
        _messageThreadId = GetCurrentThreadId();

        // Create a message-only window
        _hwnd = CreateWindowEx(0, "STATIC", "ShareX.Ava.HotkeyWindow",
            0, 0, 0, 0, 0, new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero,
            GetModuleHandle(null), IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            DebugHelper.WriteLine($"Failed to create hotkey window. Error: {Marshal.GetLastWin32Error()}");
            return;
        }

        DebugHelper.WriteLine($"Hotkey window created: 0x{_hwnd:X} on thread {_messageThreadId}");

        while (_running && GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == WM_HOTKEY)
            {
                ushort id = (ushort)msg.wParam.ToInt32();
                ProcessHotkey(id);
            }
            else if (msg.message == WM_INVOKE)
            {
                // Process queued actions on this thread
                while (_actionQueue.TryDequeue(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        DebugHelper.WriteLine($"Error executing queued hotkey action: {ex}");
                    }
                }
            }
            else
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private void ProcessHotkey(ushort id)
    {
        if (IsSuspended) return;

        // Thread-safe read
        HotkeyInfo? hotkeyInfo = null;
        lock (_lock)
        {
            if (_registeredHotkeys.TryGetValue(id, out var info))
            {
                hotkeyInfo = info;
            }
        }

        if (hotkeyInfo != null)
        {
            DebugHelper.WriteLine($"Hotkey triggered: {hotkeyInfo}");

            // Marshal to UI thread if needed
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(hotkeyInfo));
            });
        }
    }

    private void InvokeOnMessageThread(Action action)
    {
        if (_hwnd == IntPtr.Zero)
        {
            DebugHelper.WriteLine("Cannot invoke: HWND is null");
            return;
        }

        // If we're already on the message thread, just execute
        if (GetCurrentThreadId() == _messageThreadId)
        {
            action();
            return;
        }

        _actionQueue.Enqueue(action);

        // Wake up the message loop
        // We can post to the window or the thread. Posting to window is usually safer if we have it.
        bool posted = PostMessage(_hwnd, WM_INVOKE, IntPtr.Zero, IntPtr.Zero);
        if (!posted)
        {
            DebugHelper.WriteLine($"Failed to post WM_INVOKE. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    public bool RegisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (!hotkeyInfo.IsValid)
        {
            hotkeyInfo.Status = HotkeyStatus.NotConfigured;
            return false;
        }

        if (_hwnd == IntPtr.Zero)
        {
            hotkeyInfo.Status = HotkeyStatus.Failed;
            DebugHelper.WriteLine("RegisterHotkey failed: HWND not ready");
            return false;
        }

        bool result = false;
        using var mre = new ManualResetEventSlim(false);

        InvokeOnMessageThread(() =>
        {
            lock (_lock)
            {
                // Generate unique ID
                if (hotkeyInfo.Id == 0)
                {
                    hotkeyInfo.Id = _nextId++;
                }

                uint modifiers = GetModifiers(hotkeyInfo.Modifiers);
                uint vk = KeyToVirtualKey(hotkeyInfo.Key);

                // Debug log before registration attempt
                DebugHelper.WriteLine($"RegisterHotKey attempt: hwnd=0x{_hwnd:X}, id={hotkeyInfo.Id}, key={hotkeyInfo.Key} (VK=0x{vk:X2}), mods=0x{modifiers:X}");

                result = RegisterHotKey(_hwnd, hotkeyInfo.Id, modifiers, vk);

                if (result)
                {
                    hotkeyInfo.Status = HotkeyStatus.Registered;
                    _registeredHotkeys[hotkeyInfo.Id] = hotkeyInfo;
                    DebugHelper.WriteLine($"Hotkey registered: {hotkeyInfo} (ID: {hotkeyInfo.Id}, VK: 0x{vk:X2}, Mods: 0x{modifiers:X})");
                }
                else
                {
                    hotkeyInfo.Status = HotkeyStatus.Failed;
                    int error = Marshal.GetLastWin32Error();
                    // 1409 = ERROR_HOTKEY_ALREADY_REGISTERED
                    string errorMsg = error == 1409 ? "Hotkey already registered by another application" : $"Win32 error {error}";
                    DebugHelper.WriteLine($"Failed to register hotkey: {hotkeyInfo} (VK: 0x{vk:X2}, Mods: 0x{modifiers:X}) - {errorMsg}");
                }
            }
            mre.Set();
        });

        // Wait for the operation to complete
        mre.Wait(TimeSpan.FromSeconds(2));
        return result;
    }

    public bool UnregisterHotkey(HotkeyInfo hotkeyInfo)
    {
        if (hotkeyInfo.Id == 0 || _hwnd == IntPtr.Zero)
        {
            DebugHelper.WriteLine($"UnregisterHotkey: Skipped - Id={hotkeyInfo.Id}, hwnd=0x{_hwnd:X}");
            return false;
        }

        bool result = false;
        using var mre = new ManualResetEventSlim(false);

        InvokeOnMessageThread(() =>
        {
            lock (_lock)
            {
                // Safety check: is it actually registered?
                if (!_registeredHotkeys.ContainsKey(hotkeyInfo.Id))
                {
                    DebugHelper.WriteLine($"UnregisterHotkey: Ignored - ID {hotkeyInfo.Id} not found in local registry (prevents error 1419)");
                    hotkeyInfo.Status = HotkeyStatus.NotConfigured;
                    result = true; // Treat as success since it's not registered
                    mre.Set();
                    return;
                }

                DebugHelper.WriteLine($"UnregisterHotkey attempt: hwnd=0x{_hwnd:X}, id={hotkeyInfo.Id}, hotkey={hotkeyInfo}");
                result = UnregisterHotKey(_hwnd, hotkeyInfo.Id);

                if (result)
                {
                    _registeredHotkeys.Remove(hotkeyInfo.Id);
                    hotkeyInfo.Status = HotkeyStatus.NotConfigured;
                    DebugHelper.WriteLine($"Hotkey unregistered successfully: {hotkeyInfo}");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();

                    // If error is 1419 (Hot key is not registered), we should still clear it from our map to stay in sync
                    if (error == 1419)
                    {
                        DebugHelper.WriteLine($"Warning: Win32 error 1419 (Not Registered) for {hotkeyInfo}. Cleaning up local map.");
                        _registeredHotkeys.Remove(hotkeyInfo.Id);
                        hotkeyInfo.Status = HotkeyStatus.NotConfigured;
                        result = true; // Treat as success
                    }
                    else
                    {
                        hotkeyInfo.Status = HotkeyStatus.Failed;
                        DebugHelper.WriteLine($"Failed to unregister hotkey: {hotkeyInfo} - Win32 error {error}");
                    }
                }
            }
            mre.Set();
        });

        mre.Wait(TimeSpan.FromSeconds(2));
        return result;
    }

    public void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero) return;

        // Async fire and forget for UnregisterAll during shutdown
        InvokeOnMessageThread(() =>
        {
            lock (_lock)
            {
                foreach (var kvp in _registeredHotkeys)
                {
                    UnregisterHotKey(_hwnd, kvp.Key);
                    kvp.Value.Status = HotkeyStatus.NotConfigured;
                }
                _registeredHotkeys.Clear();
            }
        });
    }

    public bool IsRegistered(HotkeyInfo hotkeyInfo)
    {
        lock (_lock)
        {
            return hotkeyInfo.Id != 0 && _registeredHotkeys.ContainsKey(hotkeyInfo.Id);
        }
    }

    private static uint GetModifiers(KeyModifiers modifiers)
    {
        uint result = 0; // Don't use MOD_NOREPEAT - it can cause registration failures (Error 1408)

        if (modifiers.HasFlag(KeyModifiers.Control))
            result |= MOD_CONTROL;
        if (modifiers.HasFlag(KeyModifiers.Alt))
            result |= MOD_ALT;
        if (modifiers.HasFlag(KeyModifiers.Shift))
            result |= MOD_SHIFT;
        if (modifiers.HasFlag(KeyModifiers.Meta))
            result |= MOD_WIN;

        return result;
    }

    private static uint KeyToVirtualKey(Key key)
    {
        // Avalonia Key enum maps closely to Windows virtual key codes
        // Note: OemOpenBrackets = Oem4 = [{ and OemCloseBrackets = Oem6 = ]}
        return key switch
        {
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Return => 0x0D,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.End => 0x23,
            Key.Home => 0x24,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            >= Key.D0 and <= Key.D9 => (uint)(0x30 + (key - Key.D0)),
            >= Key.A and <= Key.Z => (uint)(0x41 + (key - Key.A)),
            >= Key.NumPad0 and <= Key.NumPad9 => (uint)(0x60 + (key - Key.NumPad0)),
            >= Key.F1 and <= Key.F24 => (uint)(0x70 + (key - Key.F1)),
            Key.PrintScreen => 0x2C,
            Key.Scroll => 0x91,
            Key.Pause => 0x13,
            Key.NumLock => 0x90,
            Key.Capital => 0x14,
            Key.Add => 0x6B,
            Key.Subtract => 0x6D,
            Key.Multiply => 0x6A,
            Key.Divide => 0x6F,
            Key.Decimal => 0x6E,
            Key.OemComma => 0xBC,
            Key.OemPeriod => 0xBE,
            Key.OemMinus => 0xBD,
            Key.OemPlus => 0xBB,
            Key.Oem1 => 0xBA, // ;:
            Key.Oem2 => 0xBF, // /?
            Key.Oem3 => 0xC0, // `~
            Key.Oem4 => 0xDB, // [{ (also known as OemOpenBrackets)
            Key.Oem5 => 0xDC, // \|
            Key.Oem6 => 0xDD, // ]} (also known as OemCloseBrackets)
            Key.Oem7 => 0xDE, // '"
            _ => (uint)key // Direct mapping for many keys
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _running = false;
        UnregisterAll();

        if (_hwnd != IntPtr.Zero)
        {
            PostMessage(_hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        _messageThread?.Join(TimeSpan.FromSeconds(2));

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
