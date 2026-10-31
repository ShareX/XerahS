import { z } from "zod";

import { MAX_EVENTS, WINDOW_HOURS } from "./analysis";

// Wire contract for POST /v1/reports. The desktop client and the importer
// produce this shape; the database re-checks the important constraints.

const text = (max: number) =>
  z
    .string()
    .max(max)
    .regex(/^[^\u0000]*$/);
const optionalText = (max: number) => text(max).nullish();
const isoTime = z.iso.datetime({ offset: true });

const display = z.object({
  ordinal: z.number().int().min(0).max(64),
  deviceName: optionalText(64),
  model: optionalText(128),
  isPrimary: z.boolean().nullish(),
  x: z.number().int().nullish(),
  y: z.number().int().nullish(),
  width: z.number().int().min(1).max(65_535),
  height: z.number().int().min(1).max(65_535),
  scale: z.number().min(0.1).max(10).nullish(),
  rotation: z.number().int().min(0).max(270).nullish(),
  refreshHz: z.number().min(1).max(1_000).nullish(),
  bitsPerPixel: z.number().int().min(1).max(64).nullish(),
  isHdr: z.boolean().nullish(),
});

const gpu = z.object({
  name: optionalText(256),
  vendorId: optionalText(16),
  deviceId: optionalText(16),
  driverVersion: optionalText(64),
  vramMbBucket: z.number().int().min(0).max(1_048_576).nullish(),
  isSoftwareRenderer: z.boolean().nullish(),
});

const session = z.object({
  startedAt: isoTime,
  endedAt: isoTime.nullish(),
  appVersion: optionalText(64),
  exitKind: z.enum(["clean", "abnormal", "running", "unknown"]),
  lastComponent: optionalText(128),
  lastMessageTemplate: optionalText(2_000),
});

const event = z.object({
  kind: z.enum(["exception", "abnormal_exit", "error_line"]),
  component: optionalText(128),
  exceptionType: optionalText(256),
  messageTemplate: text(2_000),
  sampleMessage: optionalText(4_000),
  topFrames: z.array(text(1_000)).max(30).default([]),
  firstAt: isoTime.nullish(),
  lastAt: isoTime.nullish(),
  occurrences: z.number().int().min(1).max(10_000_000).default(1),
});

const logFile = z.object({
  fileName: text(128).regex(/^[^\\/\p{Cc}]+$/u),
  content: z.string().max(16 * 1024 * 1024),
});

/** Uncompressed size cap for all log text in one report. */
export const MAX_LOG_BYTES = 24 * 1024 * 1024;

export const reportSchema = z
  .object({
    schemaVersion: z.literal(1),
    redactionVersion: z.number().int().min(1).max(1_000),
    trigger: z.enum(["manual", "after_crash"]),
    installId: z.uuid(),
    clientReportId: z.uuid(),
    comment: optionalText(2_000),
    app: z.object({
      version: text(64).min(1),
      buildFlavor: text(32).min(1),
      dotnetVersion: optionalText(64),
    }),
    os: z.object({
      family: z.enum(["windows", "linux", "macos", "other"]),
      description: optionalText(256),
      version: optionalText(64),
      architecture: optionalText(32),
      processArchitecture: optionalText(32),
      elevated: z.boolean().nullish(),
      sandbox: optionalText(32),
      sessionType: optionalText(32),
      desktopEnvironment: optionalText(64),
      uiLanguage: optionalText(16),
    }),
    capture: z
      .object({
        captureBackend: optionalText(64),
        recordingBackend: optionalText(128),
        ffmpeg: z
          .object({
            version: optionalText(128),
            source: optionalText(32),
            hwEncoders: z.array(text(64)).max(64).default([]),
          })
          .default({ hwEncoders: [] }),
      })
      .default({ ffmpeg: { hwEncoders: [] } }),
    hardware: z
      .object({
        cpuModel: optionalText(256),
        logicalCores: z.number().int().min(1).max(4_096).nullish(),
        ramGbBucket: z.number().int().min(0).max(65_536).nullish(),
        gpus: z.array(gpu).max(16).default([]),
      })
      .default({ gpus: [] }),
    displays: z.array(display).max(32).default([]),
    window: z.object({
      kind: z.enum(["24h", "7d"]),
      start: isoTime,
      end: isoTime,
    }),
    sessions: z.array(session).max(1_000).default([]),
    events: z.array(event).max(MAX_EVENTS).default([]),
    logs: z.array(logFile).max(64).default([]),
    extra: z.record(z.string(), z.unknown()).default({}),
  })
  .superRefine((report, ctx) => {
    const start = Date.parse(report.window.start);
    const end = Date.parse(report.window.end);
    // Local wall-clock logs can be a day off UTC; allow that much slack.
    const slackMs = 26 * 3_600_000;
    if (end < start) {
      ctx.addIssue({
        code: "custom",
        path: ["window"],
        message: "The window ends before it starts.",
      });
    }
    if (end - start > WINDOW_HOURS[report.window.kind] * 3_600_000 + slackMs) {
      ctx.addIssue({
        code: "custom",
        path: ["window"],
        message: `The window is longer than ${report.window.kind}.`,
      });
    }
    if (end > Date.now() + slackMs) {
      ctx.addIssue({
        code: "custom",
        path: ["window", "end"],
        message: "The window ends in the future.",
      });
    }
    const logBytes = report.logs.reduce(
      (sum, log) => sum + Buffer.byteLength(log.content),
      0,
    );
    if (logBytes > MAX_LOG_BYTES) {
      ctx.addIssue({
        code: "custom",
        path: ["logs"],
        message: "The logs are too large.",
      });
    }
  });

export type DiagnosticsReport = z.infer<typeof reportSchema>;
