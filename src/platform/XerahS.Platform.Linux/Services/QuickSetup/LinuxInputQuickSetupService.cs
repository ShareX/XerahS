// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) 2026 ShareX Team.
// Linux implementation of IHotkeyAccessSetupService: grants the user read
// access to their keyboards through polkit, then moves hotkeys to evdev.
using XerahS.Platform.Abstractions;

namespace XerahS.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxInputQuickSetupService : IHotkeyAccessSetupService
{
    private const string UnexpectedFailureMessage =
        "Quick Setup could not be launched. Check that a polkit authentication agent is running and try again.";

    private readonly SwitchableHotkeyService _hotkeys;
    private readonly bool _backendForced;
    private readonly IPrivilegedHostCommandLauncher _launcher;
    private readonly LinuxQuickSetupExecutor _executor;
    private readonly Func<IReadOnlyList<string>> _findKeyboardsNeedingAccess;
    private readonly Func<bool> _evdevAvailable;
    private readonly Func<IHotkeyService> _createEvdevService;
    private readonly Func<LinuxDistroFamily> _detectDistro;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private IHotkeyService? _evdevService;

    /// <param name="hotkeys">The hotkey service registered with the platform.</param>
    /// <param name="backendForced">True when XERAHS_LINUX_HOTKEY_BACKEND pins the backend.</param>
    public LinuxInputQuickSetupService(SwitchableHotkeyService hotkeys, bool backendForced)
        : this(
            hotkeys,
            backendForced,
            new DirectPolkitHostCommandLauncher(),
            new LinuxQuickSetupExecutor(),
            KeyboardDeviceLocator.FindKeyboardsNeedingAccess,
            EvdevGlobalHotkeyService.IsAvailable,
            () => new EvdevGlobalHotkeyService(),
            LinuxDistroGuidance.Detect)
    {
    }

    internal LinuxInputQuickSetupService(
        SwitchableHotkeyService hotkeys,
        bool backendForced,
        IPrivilegedHostCommandLauncher launcher,
        LinuxQuickSetupExecutor executor,
        Func<IReadOnlyList<string>> findKeyboardsNeedingAccess,
        Func<bool> evdevAvailable,
        Func<IHotkeyService> createEvdevService,
        Func<LinuxDistroFamily> detectDistro)
    {
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        _backendForced = backendForced;
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _findKeyboardsNeedingAccess = findKeyboardsNeedingAccess ?? throw new ArgumentNullException(nameof(findKeyboardsNeedingAccess));
        _evdevAvailable = evdevAvailable ?? throw new ArgumentNullException(nameof(evdevAvailable));
        _createEvdevService = createEvdevService ?? throw new ArgumentNullException(nameof(createEvdevService));
        _detectDistro = detectDistro ?? throw new ArgumentNullException(nameof(detectDistro));
    }

    /// <summary>
    /// Offered only when the active backend reports a delivery problem, the backend is not pinned, and
    /// either keyboards are waiting for access or access already exists and only the switch is missing.
    /// A working portal or X11 session never prompts for keyboard access.
    /// </summary>
    public bool IsSetupRecommended
    {
        get
        {
            if (_backendForced || UsesEvdev)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_hotkeys.GetDiagnostics().UserFacingWarning))
            {
                return false;
            }

            return _evdevAvailable() || _findKeyboardsNeedingAccess().Count > 0;
        }
    }

    public string SetupDescription =>
        "Let XerahS read your keyboard directly so hotkeys work in every app. " +
        "Your system will ask for an administrator password once. " +
        "Only keyboards are granted, read-only, until the next reboot.";

    public async Task<HotkeyAccessSetupResult> RunSetupAsync(CancellationToken cancellationToken = default)
    {
        await _runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<string> keyboards = _findKeyboardsNeedingAccess();

            if (keyboards.Count == 0)
            {
                if (_evdevAvailable())
                {
                    SwitchToEvdev();
                    return new HotkeyAccessSetupResult(true, "XerahS can already read your keyboard. Hotkeys now use it directly.");
                }

                return new HotkeyAccessSetupResult(false, "No keyboard was found that needs access. Run 'xerahs doctor --linux-input' for details.");
            }

            QuickSetupResult result = await _executor.RunAsync(
                _launcher,
                keyboards,
                logContext: nameof(LinuxInputQuickSetupService),
                unexpectedFailureMessage: UnexpectedFailureMessage,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                return new HotkeyAccessSetupResult(false, result.Message);
            }

            if (!_evdevAvailable())
            {
                return new HotkeyAccessSetupResult(
                    false,
                    $"{result.Message} XerahS still cannot read a keyboard. Run 'xerahs doctor --linux-input' for details.");
            }

            SwitchToEvdev();
            return new HotkeyAccessSetupResult(
                true,
                $"{result.Message} Hotkeys now work everywhere. {LinuxDistroGuidance.PersistentAccessHint(_detectDistro())}");
        }
        finally
        {
            _runLock.Release();
        }
    }

    private bool UsesEvdev
    {
        get
        {
            IHotkeyService backend = _hotkeys.Backend;
            return backend is EvdevGlobalHotkeyService || ReferenceEquals(backend, _evdevService);
        }
    }

    private void SwitchToEvdev()
    {
        if (!UsesEvdev)
        {
            _evdevService = _createEvdevService();
            _hotkeys.SwitchTo(_evdevService);
        }
    }
}
