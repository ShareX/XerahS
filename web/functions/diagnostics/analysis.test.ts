import { describe, expect, it } from "vitest";

import {
  analyzeLogs,
  collectIdentifiers,
  isErrorLine,
  normalizeTemplate,
  parseLog,
  scrub,
  splitComponent,
  timestampHoursBefore,
  windowKindFor,
} from "./analysis";

// Shaped like a real Windows 11 x64 log from 2026-09-29: a silent native
// crash inside DXGI duplication, a restart that notices hidden cursors, a
// user exit, then a downgrade.
const windowsLog = [
  "\uFEFF2026-09-29 11:06:33.290 - Executing job: RectangleRegion",
  "2026-09-29 11:06:33.402 - DxgiOutputDuplicationHelper: DuplicateOutput1 failed, using DuplicateOutput. HRESULT: [0x80070057], ApiCode: [E_INVALIDARG/Invalid arguments], Message: [The parameter is incorrect.",
  "]",
  '2026-09-29 11:49:00.978 - [PathTrace 4a33bdcf3b664d9db8632e862e4b7ec7] SaveImageToFile: dir="C:\\Users\\Dream\\OneDrive - Contoso Ltd\\Pictures\\Screenshots"',
  "2026-09-29 12:09:03.898 - CaptureFullScreenDxgi: EnumDisplaySettings orientation for \\\\.\\DISPLAY4 => dmDisplayOrientation=3, mappedRotation=Rotate270",
  "2026-09-29 12:11:48.698 - XerahS starting.",
  "2026-09-29 12:11:48.699 - Version: 0.31.3",
  "2026-09-29 12:11:48.699 - Build: Release",
  "2026-09-29 12:11:48.701 - Operating system: Microsoft Windows 10.0.26200 (X64)",
  "2026-09-29 12:11:48.701 - .NET version: 10.0.12",
  "2026-09-29 12:11:48.702 - Running as elevated process: False",
  "2026-09-29 12:11:49.203 - [SettingsManager] UploadersConfig load started: C:\\Users\\Dream\\Documents\\XerahS\\Settings\\UploadersConfig-DREAM.json",
  "2026-09-29 12:11:49.409 - System cursors were left hidden by a previous session; restoring them.",
  "2026-09-29 12:11:51.595 - Uploaded to https://i.example-host.com/a/B1gSecret.png for dream@example.com with token=abcd1234efgh",
  "2026-09-29 12:11:52.034 - [OverlayWindow.OnOpened] \\\\.\\DISPLAY4: Logical=(1440.0x2560.0) Position=3840, -224 PhysicalTopLeft=(3840,-224) PhysicalSize=(1440x2560) MonitorPhysical=(1440x2560) ScreenAt=1440x2560 Scale=1.0000 IsPrimary=False",
  "2026-09-29 12:11:52.035 - [OverlayWindow.OnOpened] \\\\.\\DISPLAY2: Logical=(2560.0x1440.0) Position=0, 0 PhysicalTopLeft=(0,0) PhysicalSize=(3840x2160) MonitorPhysical=(3840x2160) ScreenAt=3840x2160 Scale=1.5000 IsPrimary=True",
  "2026-09-29 12:11:52.100 - Screen capture: DXGI Output Duplication succeeded (9120x3984)",
  "2026-09-29 12:11:53.000 - Unhandled exception in worker:",
  "System.NullReferenceException: Object reference not set to an instance of an object.",
  "   at XerahS.Core.Tasks.WorkerTask.Run() in /home/runner/work/XerahS/src/WorkerTask.cs:line 42",
  "   at XerahS.Core.Tasks.TaskManager.Start(WorkerTask task)",
  "2026-09-29 12:11:54.000 - [Plugins] Complete: 5 succeeded, 0 failed",
  "2026-09-29 13:50:38.889 - Tray: Exit",
  "2026-09-29 13:51:05.464 - XerahS starting.",
  "2026-09-29 13:51:05.466 - Version: 0.25.5",
  "2026-09-29 14:33:42.253 - [DestinationSettings] Initialize skipped (already initialized).",
].join("\r\n");

describe("scrub", () => {
  const identifiers = collectIdentifiers(windowsLog);

  it("collects user, machine and org names but not distro host names", () => {
    expect(identifiers).toEqual(
      expect.arrayContaining(["Dream", "DREAM", "Contoso Ltd"]),
    );
    expect(
      collectIdentifiers("UploadersConfig-fedora.json /home/runner/x"),
    ).toEqual([]);
  });

  it("removes personal data", () => {
    const text = scrub(windowsLog, identifiers);
    expect(text).not.toMatch(
      /dream|contoso|example\.com\b|B1gSecret|abcd1234efgh/i,
    );
    expect(text).toContain("C:\\Users\\<user>\\OneDrive - <org>\\Pictures");
    expect(text).toContain("UploadersConfig-<id>.json");
    expect(text).toContain("https://i.example-host.com/<path>");
    expect(text).toContain("<email>");
    expect(text).toContain("token=<redacted>");
  });

  it("does not mistake version numbers for addresses", () => {
    expect(
      scrub(
        "Version: 0.31.3.12 Dev, plugin v1.2.3.4, peer 203.0.113.9, VERSION=2.0.1.7",
      ),
    ).toBe(
      "Version: 0.31.3.12 Dev, plugin v1.2.3.4, peer <ip>, VERSION=2.0.1.7",
    );
  });

  it("keeps public URLs, versions and loopback", () => {
    const text = scrub(
      "GET https://api.github.com/repos/KovaForge/XerahS/releases?x=1 at 127.0.0.1 on 10.0.26200 from 203.0.113.9",
    );
    expect(text).toBe(
      "GET https://api.github.com/repos/KovaForge/XerahS/releases at 127.0.0.1 on 10.0.26200 from <ip>",
    );
  });
});

describe("normalizeTemplate", () => {
  it("keeps identifiers and HRESULTs and collapses volatile values", () => {
    expect(
      normalizeTemplate(
        "DuplicateOutput1 failed HRESULT [0x80070057] hwnd=0x17103C took 25 ms on \\\\.\\DISPLAY4 Rotate270",
      ),
    ).toBe(
      "DuplicateOutput1 failed HRESULT [0x80070057] hwnd=<hex> took # ms on <display> Rotate270",
    );
    expect(
      normalizeTemplate(
        "saved C:\\Users\\<user>\\a.png and /home/<user>/b.png id 4a33bdcf3b664d9db8632e862e4b7ec7",
      ),
    ).toBe("saved <path> and <path> id <guid>");
  });
});

describe("parsing helpers", () => {
  it("joins continuation lines and drops a repeated timestamp", () => {
    const entries = parseLog(
      "2026-03-01 03:52:22.373 - 2026-03-01 03:52:22.373 - XerahS starting.\n  detail",
      "a.log",
    );
    expect(entries).toEqual([
      {
        timestamp: "2026-03-01 03:52:22.373",
        message: "XerahS starting.",
        details: ["  detail"],
        fileName: "a.log",
      },
    ]);
  });

  it("splits components", () => {
    expect(splitComponent("[FFmpeg] Found it")).toEqual({
      component: "FFmpeg",
      text: "Found it",
    });
    expect(splitComponent("CaptureFullScreenDxgi: Output ok")).toEqual({
      component: "CaptureFullScreenDxgi",
      text: "Output ok",
    });
    expect(splitComponent("Hotkey triggered: Print Screen")).toEqual({
      component: null,
      text: "Hotkey triggered: Print Screen",
    });
  });

  it("recognises error lines but not success counters", () => {
    expect(isErrorLine("DuplicateOutput1 failed, using DuplicateOutput")).toBe(
      true,
    );
    expect(isErrorLine("[Plugins] Complete: 5 succeeded, 0 failed")).toBe(
      false,
    );
    expect(isErrorLine("Hotkey registered: Print Screen")).toBe(false);
  });

  it("picks a window kind from the span", () => {
    expect(
      windowKindFor("2026-09-29 00:00:00.000", "2026-09-29 23:00:00.000"),
    ).toBe("24h");
    expect(
      windowKindFor("2026-09-25 00:00:00.000", "2026-09-29 00:00:00.000"),
    ).toBe("7d");
    expect(timestampHoursBefore("2026-09-29 14:00:00.000", 168)).toBe(
      "2026-09-22 14:00:00.000",
    );
  });
});

describe("analyzeLogs", () => {
  const result = analyzeLogs([
    { fileName: "XerahS-20260929.log", content: windowsLog },
  ]);

  it("classifies sessions", () => {
    expect(result.sessions.map((s) => [s.appVersion, s.exitKind])).toEqual([
      [null, "abnormal"], // next start found hidden cursors
      ["0.31.3", "clean"], // Tray: Exit
      ["0.25.5", "unknown"], // newest, still running when exported
    ]);
    expect(result.sessions[0]?.lastComponent).toBe("CaptureFullScreenDxgi");
    expect(result.sessions[0]?.lastMessageTemplate).toBe(
      "EnumDisplaySettings orientation for <display> => dmDisplayOrientation=#, mappedRotation=Rotate270",
    );
  });

  it("marks the reporting process as running", () => {
    const live = analyzeLogs([{ fileName: "x.log", content: windowsLog }], {
      lastSessionIsCurrentProcess: true,
    });
    expect(live.sessions.at(-1)?.exitKind).toBe("running");
  });

  it("extracts ranked events", () => {
    expect(result.events.map((e) => e.kind)).toEqual([
      "abnormal_exit",
      "exception",
      "error_line",
    ]);
    const exception = result.events[1];
    expect(exception?.exceptionType).toBe("System.NullReferenceException");
    expect(exception?.topFrames).toEqual([
      "XerahS.Core.Tasks.WorkerTask.Run()",
      "XerahS.Core.Tasks.TaskManager.Start(WorkerTask task)",
    ]);
    expect(result.events[2]?.messageTemplate).toMatch(
      /^DuplicateOutput1 failed, using DuplicateOutput\. HRESULT: \[0x80070057\]/,
    );
  });

  it("recovers facts and the monitor layout", () => {
    expect(result.facts).toMatchObject({
      appVersion: "0.25.5",
      buildFlavor: "Release",
      osFamily: "windows",
      osArch: "X64",
      dotnetVersion: "10.0.12",
      elevated: false,
      captureBackend: "dxgi",
    });
    expect(result.facts.displays).toEqual([
      {
        ordinal: 0,
        deviceName: "\\\\.\\DISPLAY2",
        isPrimary: true,
        x: 0,
        y: 0,
        width: 3840,
        height: 2160,
        scale: 1.5,
        rotation: null,
      },
      {
        ordinal: 1,
        deviceName: "\\\\.\\DISPLAY4",
        isPrimary: false,
        x: 3840,
        y: -224,
        width: 1440,
        height: 2560,
        scale: 1,
        rotation: 270,
      },
    ]);
  });

  it("filters to the window and scrubs stored log text", () => {
    const recent = analyzeLogs([{ fileName: "x.log", content: windowsLog }], {
      sinceTimestamp: "2026-09-29 13:00:00.000",
    });
    expect(recent.entries).toHaveLength(4);
    expect(result.logs[0]?.content).not.toMatch(/dream|contoso/i);
  });
});
