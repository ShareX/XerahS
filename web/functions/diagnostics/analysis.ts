// Reference implementation of XerahS log scrubbing and analysis.
//
// The desktop client (XerahS.Core Diagnostics) mirrors these rules so reports
// look the same whether they come from the app, the ingest Function's
// server-side re-scrub, or scripts/import-diagnostics-log.ts. Keep this file
// free of imports and non-erasable TypeScript so Node can run it directly.

export const REDACTION_VERSION = 1;
export const SCHEMA_VERSION = 1;

export const MAX_EVENTS = 500;
export const MAX_SAMPLE_LENGTH = 500;
export const MAX_FRAMES = 30;

/** Marker the app writes on orderly shutdown from v0.32.0 on. */
export const CLEAN_EXIT_MESSAGE = "XerahS exiting.";
export const START_MESSAGE = "XerahS starting.";
/** First version that writes CLEAN_EXIT_MESSAGE, so a missing marker means a crash. */
export const EXIT_MARKER_VERSION = [0, 32, 0] as const;

export type EventKind = "exception" | "abnormal_exit" | "error_line";
export type ExitKind = "clean" | "abnormal" | "running" | "unknown";
export type WindowKind = "24h" | "7d";

/** Log history a report may cover. The server rejects anything else. */
export const WINDOW_HOURS: Record<WindowKind, number> = {
  "24h": 24,
  "7d": 24 * 7,
};

export interface LogEntry {
  /** Local wall-clock time as written by the app, "yyyy-MM-dd HH:mm:ss.fff". */
  timestamp: string;
  message: string;
  /** Continuation lines (exception text, stack frames). */
  details: string[];
  fileName: string;
}

export interface SessionSummary {
  startedAt: string;
  endedAt: string | null;
  appVersion: string | null;
  exitKind: ExitKind;
  lastComponent: string | null;
  lastMessageTemplate: string | null;
}

export interface EventSummary {
  kind: EventKind;
  component: string | null;
  exceptionType: string | null;
  messageTemplate: string;
  sampleMessage: string | null;
  topFrames: string[];
  firstAt: string | null;
  lastAt: string | null;
  occurrences: number;
}

export interface DisplaySummary {
  ordinal: number;
  deviceName: string;
  isPrimary: boolean;
  x: number | null;
  y: number | null;
  width: number;
  height: number;
  scale: number;
  rotation: number | null;
}

// ---------------------------------------------------------------------------
// Scrubbing
// ---------------------------------------------------------------------------

const PUBLIC_URL_HOSTS = new Set([
  "github.com",
  "api.github.com",
  "raw.githubusercontent.com",
  "xerahs.com",
  "cloud.xerahs.com",
  "getsharex.com",
]);

const SECRET_KEY =
  /\b(authorization|bearer|api[_-]?key|apikey|access[_-]?token|refresh[_-]?token|client[_-]?secret|secret|password|passwd|token|cookie|sig|signature)(\s*[:=]\s*|\s+)("?)([^\s"',;&]{4,})/gi;

/**
 * Names that identify the user or machine: home-folder user names and the
 * machine suffix XerahS puts on per-machine settings files.
 */
export function collectIdentifiers(text: string): string[] {
  const names = new Set<string>();
  const add = (value: string | undefined) => {
    const name = value?.trim();
    if (!name || name.length < 3) return;
    if (
      /^(public|default|shared|user|users|home|root|admin|<user>|<machine>)$/i.test(
        name,
      )
    )
      return;
    // Default host names are distro or product names: not identifying, and
    // masking them would also mangle the OS description.
    if (
      /^(fedora|ubuntu|debian|arch|archlinux|manjaro|endeavouros|nixos|pop-os|mint|linuxmint|opensuse|suse|centos|rocky|alma|gentoo|kali|zorin|elementary|garuda|cachyos|bazzite|steamdeck|localhost|runner|vsts|appveyor|builder|desktop|laptop|computer|windows|linux|macbook|imac|omarchy)$/i.test(
        name,
      )
    )
      return;
    names.add(name);
  };
  for (const match of text.matchAll(/[A-Za-z]:\\Users\\([^\\/\r\n"'<>|]+)/g))
    add(match[1]);
  for (const match of text.matchAll(
    /\/(?:home|Users|var\/home)\/([^/\s"'<>]+)/g,
  ))
    add(match[1]);
  for (const match of text.matchAll(
    /\b[A-Za-z]+Config-([A-Za-z0-9_.-]+?)\.json\b/g,
  ))
    add(match[1]);
  for (const match of text.matchAll(/OneDrive - ([^\\/\r\n"]+)/g))
    add(match[1]);
  return [...names].sort((a, b) => b.length - a.length);
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * Removes personal data from log text. `identifiers` are extra names to
 * mask everywhere (collectIdentifiers over the whole bundle, plus the
 * machine and user names the client knows).
 */
export function scrub(
  text: string,
  identifiers: readonly string[] = [],
): string {
  let result = text;

  result = result.replace(/([A-Za-z]:\\Users\\)[^\\/\r\n"'<>|]+/g, "$1<user>");
  result = result.replace(
    /\/(home|Users|var\/home)\/[^/\s"'<>]+/g,
    "/$1/<user>",
  );
  result = result.replace(/OneDrive - [^\\/\r\n"]+/g, "OneDrive - <org>");

  result = result.replace(
    /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g,
    "<email>",
  );

  result = result.replace(
    /\b(https?):\/\/([^\s/"'<>?#]+)([^\s"'<>]*)/gi,
    (whole, scheme: string, host: string, rest: string) => {
      const bareHost = host.replace(/^[^@]*@/, "").toLowerCase();
      if (PUBLIC_URL_HOSTS.has(bareHost.split(":")[0] ?? "")) {
        return `${scheme}://${bareHost}${rest.replace(/[?#].*$/, "")}`;
      }
      return rest
        ? `${scheme}://${bareHost}/<path>`
        : `${scheme}://${bareHost}`;
    },
  );

  result = result.replace(
    SECRET_KEY,
    (_whole, key: string, sep: string, quote: string) =>
      `${key}${sep}${quote}<redacted>`,
  );

  // IPv4 addresses, but not version numbers ("Version: 0.31.3.1", "v1.2.3.4").
  result = result.replace(
    /(?<![\d.])(?<!(?:version|ver)[:=]?\s{0,3})(?<!\bv)(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}(?![\d.])/gi,
    (ip) =>
      ip === "127.0.0.1" || ip === "0.0.0.0" || ip.startsWith("0.")
        ? ip
        : "<ip>",
  );

  for (const name of identifiers) {
    if (!name || name.length < 3) continue;
    result = result.replace(
      new RegExp(`(?<![A-Za-z0-9])${escapeRegExp(name)}(?![A-Za-z0-9])`, "gi"),
      "<id>",
    );
  }

  return result;
}

// ---------------------------------------------------------------------------
// Parsing
// ---------------------------------------------------------------------------

const ENTRY = /^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) - (.*)$/;

export function parseLog(content: string, fileName: string): LogEntry[] {
  const entries: LogEntry[] = [];
  const lines = content.replace(/^﻿/, "").split(/\r?\n/);
  for (const line of lines) {
    const match = ENTRY.exec(line);
    if (match) {
      let message = match[2] ?? "";
      // Some builds wrote the timestamp twice on the first line.
      const repeated = ENTRY.exec(message);
      if (repeated && repeated[1] === match[1]) message = repeated[2] ?? "";
      entries.push({
        timestamp: match[1] ?? "",
        message,
        details: [],
        fileName,
      });
    } else if (entries.length > 0 && line.length > 0) {
      entries[entries.length - 1]?.details.push(line);
    }
  }
  return entries;
}

export function compareVersions(
  a: readonly number[],
  b: readonly number[],
): number {
  for (let i = 0; i < Math.max(a.length, b.length); i++) {
    const diff = (a[i] ?? 0) - (b[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

export function parseVersion(
  value: string | null | undefined,
): number[] | null {
  const match = /^(\d+)\.(\d+)(?:\.(\d+))?/.exec(value?.trim() ?? "");
  return match
    ? [Number(match[1]), Number(match[2]), Number(match[3] ?? 0)]
    : null;
}

/** Splits "[Tag] text" or "TypeName: text" into component and message. */
export function splitComponent(message: string): {
  component: string | null;
  text: string;
} {
  const tag = /^\[([^\]]{1,64})\]\s*(.*)$/.exec(message);
  if (tag) return { component: tag[1] ?? null, text: tag[2] ?? "" };
  const prefix = /^([A-Za-z][A-Za-z0-9_.]{2,63}):\s+(.*)$/.exec(message);
  if (prefix) return { component: prefix[1] ?? null, text: prefix[2] ?? "" };
  return { component: null, text: message };
}

/**
 * Collapses volatile tokens so one failure groups across machines and runs.
 * Keeps identifiers (DuplicateOutput1, Rotate270) and HRESULT failure codes.
 * The database applies the same rules again (diagnostics.normalize_template).
 */
export function normalizeTemplate(text: string): string {
  return (
    text
      .replace(/\\\\\.\\DISPLAY\d+/g, "<display>")
      // Paths may already contain scrub placeholders such as <user>.
      .replace(/[A-Za-z]:\\[^\s"'|,;)\]]+/g, "<path>")
      .replace(
        /(?<![\w/.<>-])\/(?:home|Users|usr|tmp|opt|var|run|etc|mnt|media|srv|proc|dev|nix)\/[^\s"',;)\]]*/g,
        "<path>",
      )
      .replace(
        /[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/g,
        "<guid>",
      )
      .replace(/\b[0-9a-f]{32}\b/g, "<guid>")
      .replace(/\b0x(?![89a-fA-F][0-9a-fA-F]{7}\b)[0-9a-fA-F]+\b/g, "<hex>")
      .replace(/\b\d+(?:\.\d+)*\b/g, "#")
      .replace(/\s+/g, " ")
      .trim()
      .slice(0, 2000)
  );
}

const EXCEPTION_HEADER =
  /^\s*(?:---> )?((?:[A-Za-z_][\w]*\.)*[A-Za-z_]\w*(?:Exception|Error))(?::\s*(.*))?$/;
const STACK_FRAME = /^\s+at (.+?)(?: in .+?:line \d+)?\s*$/;

interface ExceptionInfo {
  type: string;
  message: string;
  frames: string[];
}

export function findException(entry: LogEntry): ExceptionInfo | null {
  const candidates = [entry.message, ...entry.details];
  for (let i = 0; i < candidates.length; i++) {
    const header = EXCEPTION_HEADER.exec(candidates[i] ?? "");
    if (!header) continue;
    const frames: string[] = [];
    for (const line of entry.details) {
      const frame = STACK_FRAME.exec(line);
      if (frame?.[1]) frames.push(frame[1].replace(/\s+/g, " "));
      if (frames.length >= MAX_FRAMES) break;
    }
    return { type: header[1] ?? "Exception", message: header[2] ?? "", frames };
  }
  // "Unhandled AppDomain exception:\nSystem.X: msg" style entries.
  const inline =
    /((?:[A-Za-z_]\w*\.)+[A-Za-z_]\w*(?:Exception|Error)):\s*(.*)$/.exec(
      entry.message,
    );
  if (inline)
    return {
      type: inline[1] ?? "Exception",
      message: inline[2] ?? "",
      frames: [],
    };
  return null;
}

const ERROR_WORDS =
  /\b(fail(?:ed|ure|s)?|error|exception|crash(?:ed)?|unable to|could not|cannot|access violation|timed out)\b/i;
const NOT_AN_ERROR =
  /\b(0 failed|no errors?|without errors?|0 errors?|errors?=0|failed=0)\b/i;

export function isErrorLine(message: string): boolean {
  return ERROR_WORDS.test(message) && !NOT_AN_ERROR.test(message);
}

/** Evidence, written at start-up, that the previous process died. */
const PREVIOUS_SESSION_DIED = [
  /System cursors were left hidden by a previous session/i,
  /previous session (?:crashed|ended unexpectedly|did not exit cleanly)/i,
];
const PROCESS_TERMINATING = [
  /Unhandled AppDomain exception terminating=True/i,
  /Critical application startup failure/i,
];
const ORDERLY_SHUTDOWN = [
  /^Tray: Exit$/,
  /Installer launched\. Shutting down application/i,
  /^Exiting \(--exit-on-complete specified\)\./,
  /^Startup wait elapsed\. Exiting after collecting diagnostics\./,
];

export interface AnalyzeOptions {
  /** True when the newest session is the process doing the reporting. */
  lastSessionIsCurrentProcess?: boolean;
}

export function toSessions(
  entries: readonly LogEntry[],
  options: AnalyzeOptions = {},
): Array<SessionSummary & { entries: LogEntry[] }> {
  const sessions: Array<SessionSummary & { entries: LogEntry[] }> = [];
  let current: (SessionSummary & { entries: LogEntry[] }) | null = null;
  for (const entry of entries) {
    if (entry.message === START_MESSAGE) {
      current = {
        startedAt: entry.timestamp,
        endedAt: null,
        appVersion: null,
        exitKind: "unknown",
        lastComponent: null,
        lastMessageTemplate: null,
        entries: [],
      };
      sessions.push(current);
    }
    if (!current) {
      // Log text from before the first start line in the window.
      current = {
        startedAt: entry.timestamp,
        endedAt: null,
        appVersion: null,
        exitKind: "unknown",
        lastComponent: null,
        lastMessageTemplate: null,
        entries: [],
      };
      sessions.push(current);
    }
    current.entries.push(entry);
    if (!current.appVersion) {
      const version = /^Version: (\S+)/.exec(entry.message);
      if (version) current.appVersion = version[1] ?? null;
    }
  }

  sessions.forEach((session, index) => {
    const last = session.entries[session.entries.length - 1];
    session.endedAt = last?.timestamp ?? session.startedAt;
    const tail =
      [...session.entries]
        .reverse()
        .find((e) => e.message !== CLEAN_EXIT_MESSAGE) ?? last;
    if (tail) {
      const { component, text } = splitComponent(tail.message);
      session.lastComponent = component;
      session.lastMessageTemplate = normalizeTemplate(text);
    }

    const next = sessions[index + 1];
    const isLast = index === sessions.length - 1;
    const messages = session.entries.map((e) => e.message);
    const version = parseVersion(session.appVersion);

    if (
      messages.includes(CLEAN_EXIT_MESSAGE) ||
      messages.some((m) => ORDERLY_SHUTDOWN.some((p) => p.test(m)))
    ) {
      session.exitKind = "clean";
    } else if (
      messages.some((m) => PROCESS_TERMINATING.some((p) => p.test(m)))
    ) {
      session.exitKind = "abnormal";
    } else if (isLast) {
      session.exitKind = options.lastSessionIsCurrentProcess
        ? "running"
        : "unknown";
    } else if (
      next?.entries.some((e) =>
        PREVIOUS_SESSION_DIED.some((p) => p.test(e.message)),
      )
    ) {
      session.exitKind = "abnormal";
    } else if (version && compareVersions(version, EXIT_MARKER_VERSION) >= 0) {
      session.exitKind = "abnormal";
    } else {
      session.exitKind = "unknown";
    }
  });

  return sessions;
}

export function extractEvents(
  sessions: ReadonlyArray<SessionSummary & { entries: LogEntry[] }>,
): EventSummary[] {
  const byKey = new Map<string, EventSummary>();
  const add = (
    event: Omit<EventSummary, "occurrences" | "firstAt" | "lastAt">,
    at: string,
  ) => {
    const key = [
      event.kind,
      event.exceptionType ?? "",
      event.component ?? "",
      event.messageTemplate,
      event.topFrames.slice(0, 5).join("|"),
    ].join("\u001f");
    const existing = byKey.get(key);
    if (existing) {
      existing.occurrences += 1;
      if (!existing.firstAt || at < existing.firstAt) existing.firstAt = at;
      if (!existing.lastAt || at > existing.lastAt) existing.lastAt = at;
    } else {
      byKey.set(key, { ...event, firstAt: at, lastAt: at, occurrences: 1 });
    }
  };

  for (const session of sessions) {
    for (const entry of session.entries) {
      const { component, text } = splitComponent(entry.message);
      const exception = findException(entry);
      if (exception) {
        add(
          {
            kind: "exception",
            component,
            exceptionType: exception.type,
            messageTemplate: normalizeTemplate(exception.message || text),
            sampleMessage:
              (exception.message || text).slice(0, MAX_SAMPLE_LENGTH) || null,
            topFrames: exception.frames,
          },
          entry.timestamp,
        );
      } else if (isErrorLine(entry.message)) {
        add(
          {
            kind: "error_line",
            component,
            exceptionType: null,
            messageTemplate: normalizeTemplate(text),
            sampleMessage:
              [text, ...entry.details].join(" ").slice(0, MAX_SAMPLE_LENGTH) ||
              null,
            topFrames: [],
          },
          entry.timestamp,
        );
      }
    }

    if (session.exitKind === "abnormal") {
      add(
        {
          kind: "abnormal_exit",
          component: session.lastComponent,
          exceptionType: null,
          messageTemplate: session.lastMessageTemplate ?? "",
          sampleMessage:
            session.entries[session.entries.length - 1]?.message.slice(
              0,
              MAX_SAMPLE_LENGTH,
            ) ?? null,
          topFrames: [],
        },
        session.endedAt ?? session.startedAt,
      );
    }
  }

  const rank = (e: EventSummary) =>
    e.kind === "abnormal_exit" ? 0 : e.kind === "exception" ? 1 : 2;
  return [...byKey.values()]
    .sort((a, b) => rank(a) - rank(b) || b.occurrences - a.occurrences)
    .slice(0, MAX_EVENTS);
}

// ---------------------------------------------------------------------------
// Facts recoverable from the log itself (used for imports of old logs)
// ---------------------------------------------------------------------------

export interface LogFacts {
  appVersion: string | null;
  buildFlavor: string | null;
  dotnetVersion: string | null;
  osDescription: string | null;
  osFamily: "windows" | "linux" | "macos" | "other";
  osArch: string | null;
  elevated: boolean | null;
  sessionType: string | null;
  sandbox: string | null;
  captureBackend: string | null;
  recordingBackend: string | null;
  ffmpegSource: string | null;
  displays: DisplaySummary[];
}

export function extractFacts(entries: readonly LogEntry[]): LogFacts {
  const facts: LogFacts = {
    appVersion: null,
    buildFlavor: null,
    dotnetVersion: null,
    osDescription: null,
    osFamily: "other",
    osArch: null,
    elevated: null,
    sessionType: null,
    sandbox: null,
    captureBackend: null,
    recordingBackend: null,
    ffmpegSource: null,
    displays: [],
  };
  const displays = new Map<string, DisplaySummary>();
  const rotations = new Map<string, number>();

  // Newest values win: walk forward and overwrite.
  for (const entry of entries) {
    const m = entry.message;
    let match: RegExpExecArray | null;
    if ((match = /^Version: (\S+)/.exec(m)))
      facts.appVersion = match[1] ?? null;
    else if ((match = /^Build: (\w+)/.exec(m)))
      facts.buildFlavor = match[1] ?? null;
    else if ((match = /^\.NET version: (\S+)/.exec(m)))
      facts.dotnetVersion = match[1] ?? null;
    else if ((match = /^Running as elevated process: (True|False)/i.exec(m)))
      facts.elevated = match[1]?.toLowerCase() === "true";
    else if (
      (match =
        /^Operating system: (.+?)(?: \((X64|X86|Arm64|Arm|S390x|LoongArch64|Ppc64le|RiscV64)\))?$/i.exec(
          m,
        ))
    ) {
      facts.osDescription = match[1] ?? null;
      facts.osArch = match[2] ?? null;
      const description = (match[1] ?? "").toLowerCase();
      facts.osFamily = description.includes("windows")
        ? "windows"
        : description.includes("darwin") || description.includes("macos")
          ? "macos"
          : "linux";
    } else if ((match = /^\s*XDG_SESSION_TYPE=(\w+)/.exec(m)))
      facts.sessionType = match[1] ?? null;
    else if ((match = /Flatpak sandbox: (True)/.exec(m)))
      facts.sandbox = "flatpak";
    else if ((match = /Snap sandbox: (True)/.exec(m))) facts.sandbox = "snap";
    else if (/DXGI Output Duplication succeeded/.test(m))
      facts.captureBackend = "dxgi";
    else if (
      /Windows\.Graphics\.Capture|WGC capture succeeded/i.test(m) &&
      !facts.captureBackend
    )
      facts.captureBackend = "wgc";
    else if ((match = /Recording backend: (.+)$/.exec(m)))
      facts.recordingBackend = match[1]?.trim() ?? null;
    else if (
      /Native recording \(WGC \+ Media Foundation\) is supported/.test(m)
    )
      facts.recordingBackend = "WGC + Media Foundation";
    else if ((match = /^\[FFmpeg\] Found FFmpeg at: (.+)$/.exec(m))) {
      const path = match[1] ?? "";
      facts.ffmpegSource = /[\\/]Tools[\\/]/i.test(path) ? "bundled" : "system";
    } else if (
      (match =
        /EnumDisplaySettings orientation for (\S+) => .*mappedRotation=Rotate(\d+)/.exec(
          m,
        ))
    ) {
      rotations.set(match[1] ?? "", Number(match[2]));
    } else if (
      (match =
        /EnumDisplaySettings orientation for (\S+) => .*mappedRotation=Identity/.exec(
          m,
        ))
    ) {
      rotations.set(match[1] ?? "", 0);
    } else if (
      (match =
        /^\[OverlayWindow\.OnOpened\] (\S+): .*PhysicalTopLeft=\((-?\d+),(-?\d+)\) PhysicalSize=\((\d+)x(\d+)\).*Scale=([\d.]+) IsPrimary=(True|False)/.exec(
          m,
        ))
    ) {
      const deviceName = match[1] ?? "";
      displays.set(deviceName, {
        ordinal: 0,
        deviceName,
        isPrimary: match[7] === "True",
        x: Number(match[2]),
        y: Number(match[3]),
        width: Number(match[4]),
        height: Number(match[5]),
        scale: Number(match[6]),
        rotation: null,
      });
    }
  }

  facts.displays = [...displays.values()]
    .sort((a, b) =>
      a.deviceName.localeCompare(b.deviceName, undefined, { numeric: true }),
    )
    .map((display, index) => ({
      ...display,
      ordinal: index,
      rotation: rotations.get(display.deviceName) ?? null,
    }));
  return facts;
}

// ---------------------------------------------------------------------------
// Report assembly
// ---------------------------------------------------------------------------

export function windowKindFor(start: string, end: string): WindowKind {
  const hours =
    (Date.parse(end.replace(" ", "T") + "Z") -
      Date.parse(start.replace(" ", "T") + "Z")) /
    3_600_000;
  return hours <= WINDOW_HOURS["24h"] ? "24h" : "7d";
}

/** Local timestamp `hours` before `end`, in the log's "yyyy-MM-dd HH:mm:ss.fff" form. */
export function timestampHoursBefore(end: string, hours: number): string {
  const ms = Date.parse(end.replace(" ", "T") + "Z") - hours * 3_600_000;
  return new Date(ms).toISOString().replace("T", " ").slice(0, 23);
}

/** "yyyy-MM-dd HH:mm:ss.fff" (local, offset unknown) to ISO, applying an offset when known. */
export function toIso(timestamp: string, utcOffsetMinutes = 0): string {
  const ms =
    Date.parse(timestamp.replace(" ", "T") + "Z") - utcOffsetMinutes * 60_000;
  return new Date(ms).toISOString();
}

export interface AnalyzedLogs {
  entries: LogEntry[];
  sessions: SessionSummary[];
  events: EventSummary[];
  facts: LogFacts;
  logs: Array<{ fileName: string; content: string }>;
  windowStart: string | null;
  windowEnd: string | null;
}

/**
 * Scrubs and analyses a set of log files. Only entries at or after
 * `sinceTimestamp` ("yyyy-MM-dd HH:mm:ss.fff", local) are kept.
 */
export function analyzeLogs(
  files: ReadonlyArray<{ fileName: string; content: string }>,
  options: AnalyzeOptions & {
    sinceTimestamp?: string;
    identifiers?: readonly string[];
  } = {},
): AnalyzedLogs {
  const identifiers = [
    ...new Set([
      ...(options.identifiers ?? []),
      ...files.flatMap((f) => collectIdentifiers(f.content)),
    ]),
  ].sort((a, b) => b.length - a.length);

  const logs: Array<{ fileName: string; content: string }> = [];
  const entries: LogEntry[] = [];
  const ordered = [...files].sort((a, b) =>
    a.fileName.localeCompare(b.fileName),
  );
  for (const file of ordered) {
    const fileName = scrub(file.fileName, identifiers)
      .replace(/[\\/]/g, "_")
      .slice(0, 128);
    const parsed = parseLog(scrub(file.content, identifiers), fileName).filter(
      (e) => !options.sinceTimestamp || e.timestamp >= options.sinceTimestamp,
    );
    if (parsed.length === 0) continue;
    entries.push(...parsed);
    logs.push({
      fileName,
      content: parsed
        .map((e) => [`${e.timestamp} - ${e.message}`, ...e.details].join("\n"))
        .join("\n"),
    });
  }
  entries.sort((a, b) =>
    a.timestamp < b.timestamp ? -1 : a.timestamp > b.timestamp ? 1 : 0,
  );

  const sessions = toSessions(entries, options);
  return {
    entries,
    sessions: sessions.map((s) => ({
      startedAt: s.startedAt,
      endedAt: s.endedAt,
      appVersion: s.appVersion,
      exitKind: s.exitKind,
      lastComponent: s.lastComponent,
      lastMessageTemplate: s.lastMessageTemplate,
    })),
    events: extractEvents(sessions),
    facts: extractFacts(entries),
    logs,
    windowStart: entries[0]?.timestamp ?? null,
    windowEnd: entries[entries.length - 1]?.timestamp ?? null,
  };
}
