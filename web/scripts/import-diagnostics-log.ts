// Imports existing XerahS log files into the diagnostics knowledge base.
//
//   node scripts/import-diagnostics-log.ts [--dry-run] [--comment "..."] <log files...>
//
// Reads DATABASE_URL_UNPOOLED (or DATABASE_URL) from the environment or
// functions/diagnostics/.env.local (`neon env pull` in that directory). Logs are scrubbed with the same rules as the desktop client
// before anything leaves this machine. Re-importing the same files is a no-op.

import { createHash, randomBytes } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { basename } from "node:path";
import process from "node:process";

import pg from "pg";

import {
  analyzeLogs,
  REDACTION_VERSION,
  SCHEMA_VERSION,
  timestampHoursBefore,
  toIso,
  WINDOW_HOURS,
  windowKindFor,
} from "../functions/diagnostics/analysis.ts";

function uuidFrom(text: string): string {
  const hex = createHash("sha256").update(text).digest("hex");
  const variant = ((parseInt(hex[16] ?? "0", 16) & 0x3) | 0x8).toString(16);
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-5${hex.slice(13, 16)}-${variant}${hex.slice(17, 20)}-${hex.slice(20, 32)}`;
}

function loadEnvFile(path: string): void {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, "utf8").split(/\r?\n/)) {
    const match = /^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/.exec(line);
    if (!match?.[1] || process.env[match[1]] !== undefined) continue;
    process.env[match[1]] = (match[2] ?? "").replace(/^(['"])(.*)\1$/, "$2");
  }
}

const files: string[] = [];
let dryRun = false;
let comment: string | undefined;
const args = process.argv.slice(2);
for (let i = 0; i < args.length; i++) {
  const arg = args[i] ?? "";
  if (arg === "--dry-run") dryRun = true;
  else if (arg === "--comment") comment = args[++i];
  else files.push(arg);
}

if (files.length === 0) {
  console.error(
    "usage: node scripts/import-diagnostics-log.ts [--dry-run] [--comment text] <log files...>",
  );
  process.exit(2);
}

for (const file of files) {
  const raw = readFileSync(file, "utf8");
  const whole = analyzeLogs([{ fileName: basename(file), content: raw }]);
  // Reports cover at most the past week: keep the newest 7 days of the log.
  const analyzed = whole.windowEnd
    ? analyzeLogs([{ fileName: basename(file), content: raw }], {
        sinceTimestamp: timestampHoursBefore(
          whole.windowEnd,
          WINDOW_HOURS["7d"],
        ),
      })
    : whole;
  if (!analyzed.windowStart || !analyzed.windowEnd) {
    console.warn(`${file}: no log entries, skipped`);
    continue;
  }

  const facts = analyzed.facts;
  // A log can span upgrades and downgrades. Report the version that failed most
  // recently, else the newest one.
  const reportedVersion =
    [...analyzed.sessions]
      .reverse()
      .find((s) => s.exitKind === "abnormal" && s.appVersion)?.appVersion ??
    facts.appVersion;
  // Stable ids so a re-import is recognised as the same report.
  const installId = uuidFrom(
    `xerahs-import-install:${facts.osDescription}:${collectPersonalPath(raw)}`,
  );
  const clientReportId = uuidFrom(
    `xerahs-import-report:${createHash("sha256").update(raw).digest("hex")}`,
  );

  const report = {
    schemaVersion: SCHEMA_VERSION,
    redactionVersion: REDACTION_VERSION,
    trigger: "manual",
    installId,
    clientReportId,
    comment: comment ?? `Imported from ${analyzed.logs[0]?.fileName ?? "log"}`,
    app: {
      version: reportedVersion ?? "unknown",
      buildFlavor: facts.buildFlavor ?? "unknown",
      dotnetVersion: facts.dotnetVersion,
    },
    os: {
      family: facts.osFamily,
      description: facts.osDescription,
      version: /(\d+\.\d+\.\d+)/.exec(facts.osDescription ?? "")?.[1] ?? null,
      architecture: facts.osArch,
      processArchitecture: null,
      elevated: facts.elevated,
      sandbox: facts.sandbox,
      sessionType:
        facts.sessionType ?? (facts.osFamily === "windows" ? "windows" : null),
    },
    capture: {
      captureBackend: facts.captureBackend,
      recordingBackend: facts.recordingBackend,
      ffmpeg: { version: null, source: facts.ffmpegSource, hwEncoders: [] },
    },
    hardware: { gpus: [] },
    displays: facts.displays,
    window: {
      kind: windowKindFor(analyzed.windowStart, analyzed.windowEnd),
      start: toIso(analyzed.windowStart),
      end: toIso(analyzed.windowEnd),
    },
    sessions: analyzed.sessions.map((s) => ({
      ...s,
      startedAt: toIso(s.startedAt),
      endedAt: s.endedAt ? toIso(s.endedAt) : null,
    })),
    events: analyzed.events.map((e) => ({
      ...e,
      firstAt: e.firstAt ? toIso(e.firstAt) : null,
      lastAt: e.lastAt ? toIso(e.lastAt) : null,
    })),
    logs: analyzed.logs,
    extra: { source: "import", timestampsAreLocal: true },
  };

  const abnormal = analyzed.sessions.filter(
    (s) => s.exitKind === "abnormal",
  ).length;
  console.log(
    `${file}: ${analyzed.entries.length} entries, ${analyzed.sessions.length} sessions (${abnormal} abnormal), ` +
      `${analyzed.events.length} event groups, ${facts.displays.length} displays, app ${report.app.version}, ${facts.osDescription}`,
  );

  if (dryRun) {
    console.log(
      JSON.stringify(
        {
          ...report,
          logs: report.logs.map((l) => ({
            fileName: l.fileName,
            bytes: l.content.length,
          })),
        },
        null,
        2,
      ),
    );
    continue;
  }

  // The diagnostics database belongs to the Function's Neon project.
  loadEnvFile("functions/diagnostics/.env.local");
  const connectionString =
    process.env.DATABASE_URL_UNPOOLED ?? process.env.DATABASE_URL;
  if (!connectionString) {
    console.error(
      "DATABASE_URL_UNPOOLED or DATABASE_URL is required (run `neon env pull`).",
    );
    process.exit(2);
  }

  // pg already treats sslmode=require as verify-full; say so explicitly.
  const client = new pg.Client({
    connectionString: connectionString.replace(
      /sslmode=require\b/,
      "sslmode=verify-full",
    ),
  });
  await client.connect();
  try {
    await client.query("begin");
    // Imports are owner operations and must work while public ingest is off.
    // The switch is flipped and restored inside this transaction, so no other
    // session ever sees it change.
    const { rows: settings } = await client.query<{ allow_ingest: boolean }>(
      "select allow_ingest from diagnostics.settings where singleton for update",
    );
    const originalAllowIngest = settings[0]?.allow_ingest ?? false;
    await client.query(
      "update diagnostics.settings set allow_ingest = true where singleton",
    );
    const deleteTokenHash = createHash("sha256")
      .update(randomBytes(32))
      .digest();
    const { rows } = await client.query<{
      result: {
        reportId: string;
        duplicate: boolean;
        primarySignatureId: number | null;
      };
    }>("select diagnostics.submit_report($1::jsonb, $2, null) as result", [
      JSON.stringify(report),
      deleteTokenHash,
    ]);
    await client.query(
      "update diagnostics.settings set allow_ingest = $1 where singleton",
      [originalAllowIngest],
    );
    await client.query("commit");
    const result = rows[0]?.result;
    console.log(
      `  -> report ${result?.reportId}${result?.duplicate ? " (already imported)" : ""}, primary signature ${result?.primarySignatureId ?? "none"}`,
    );
  } catch (error) {
    await client.query("rollback").catch(() => undefined);
    throw error;
  } finally {
    await client.end();
  }
}

function collectPersonalPath(raw: string): string {
  return /Personal path: (.+)/.exec(raw)?.[1]?.trim() ?? "";
}
