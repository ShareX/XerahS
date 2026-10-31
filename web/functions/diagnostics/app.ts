import { createHash, createHmac, randomBytes } from "node:crypto";
import { gunzipSync } from "node:zlib";

import { parseTriggerDelivery } from "@neon/functions/triggers";
import { Hono } from "hono";
import { ZodError } from "zod";

import { collectIdentifiers, REDACTION_VERSION, scrub } from "./analysis";
import { reportSchema, type DiagnosticsReport } from "./schema";

/** Largest request body accepted, compressed or not. */
export const MAX_BODY_BYTES = 8 * 1024 * 1024;
/** Largest decompressed body (logs plus metadata). */
export const MAX_INFLATED_BYTES = 32 * 1024 * 1024;

export interface SubmitResult {
  reportId: string;
  receivedAt: string;
  duplicate: boolean;
  reason: "same_report" | "no_new_entries" | null;
  primarySignatureId: number | null;
  deduplicatedBefore?: string | null;
  lastLogAt?: string | null;
}

export interface InstallStatus {
  lastLogAt: string | null;
  lastReportId: string | null;
  lastReceivedAt: string | null;
  reportCount: number;
}

/** Database operations, run as the diagnostics_ingest role. */
export interface DiagnosticsStore {
  submit(
    report: DiagnosticsReport,
    deleteTokenHash: Buffer,
    networkKey: Buffer | null,
  ): Promise<SubmitResult>;
  status(installId: string): Promise<InstallStatus>;
  delete(reportId: string, deleteTokenHash: Buffer): Promise<boolean>;
  /** Partitions and NAS-style space reclamation (diagnostics.run_maintenance). */
  maintenance(trigger: string): Promise<unknown>;
}

export interface AppOptions {
  store: DiagnosticsStore;
  /** HMAC key for per-network rate limiting. The client IP is never stored. */
  networkSecret: string | undefined;
  now?: () => Date;
}

class HttpError extends Error {
  constructor(
    readonly status: 400 | 404 | 413 | 415 | 429 | 503,
    readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function sha256(value: string | Buffer): Buffer {
  return createHash("sha256").update(value).digest();
}

function clientIp(headers: Headers): string | null {
  const forwarded = headers.get("x-forwarded-for")?.split(",")[0]?.trim();
  return forwarded || headers.get("x-real-ip")?.trim() || null;
}

async function readBody(request: Request): Promise<unknown> {
  const declared = Number(request.headers.get("content-length") ?? "0");
  if (Number.isFinite(declared) && declared > MAX_BODY_BYTES) {
    throw new HttpError(413, "too_large", "The report is too large.");
  }
  const type = request.headers.get("content-type") ?? "";
  if (!type.toLowerCase().startsWith("application/json")) {
    throw new HttpError(
      415,
      "unsupported_media_type",
      "Send application/json.",
    );
  }

  const raw = Buffer.from(await request.arrayBuffer());
  if (raw.byteLength > MAX_BODY_BYTES) {
    throw new HttpError(413, "too_large", "The report is too large.");
  }

  let json: Buffer = raw;
  const encoding = request.headers
    .get("content-encoding")
    ?.toLowerCase()
    .trim();
  if (encoding === "gzip") {
    try {
      json = gunzipSync(raw, { maxOutputLength: MAX_INFLATED_BYTES });
    } catch (error) {
      if (
        error instanceof RangeError ||
        (error as { code?: string }).code === "ERR_BUFFER_TOO_LARGE"
      ) {
        throw new HttpError(413, "too_large", "The report is too large.");
      }
      throw new HttpError(
        400,
        "invalid_request",
        "The body is not valid gzip.",
      );
    }
  } else if (encoding && encoding !== "identity") {
    throw new HttpError(
      415,
      "unsupported_media_type",
      "Only gzip encoding is supported.",
    );
  }

  try {
    return JSON.parse(json.toString("utf8"));
  } catch {
    throw new HttpError(400, "invalid_request", "The body is not valid JSON.");
  }
}

/**
 * Second scrubbing pass. The client scrubs before anything leaves the
 * machine; this catches older clients and anything a rule update added.
 */
export function rescrub(report: DiagnosticsReport): DiagnosticsReport {
  const identifiers = [
    ...new Set([
      ...report.logs.flatMap((log) => collectIdentifiers(log.content)),
      ...collectIdentifiers(report.comment ?? ""),
    ]),
  ];
  const clean = (value: string) => scrub(value, identifiers);
  const cleanOptional = (value: string | null | undefined) =>
    value == null ? value : clean(value);
  return {
    ...report,
    redactionVersion: Math.max(report.redactionVersion, REDACTION_VERSION),
    comment: cleanOptional(report.comment),
    sessions: report.sessions.map((s) => ({
      ...s,
      lastMessageTemplate: cleanOptional(s.lastMessageTemplate),
    })),
    events: report.events.map((e) => ({
      ...e,
      messageTemplate: clean(e.messageTemplate),
      sampleMessage: cleanOptional(e.sampleMessage),
      topFrames: e.topFrames.map(clean),
    })),
    logs: report.logs.map((log) => ({
      fileName: clean(log.fileName),
      content: clean(log.content),
    })),
  };
}

export function createApp({
  store,
  networkSecret,
  now = () => new Date(),
}: AppOptions): Hono {
  const app = new Hono();

  app.onError((error, c) => {
    if (error instanceof HttpError) {
      return c.json(
        { error: { code: error.code, message: error.message } },
        error.status,
      );
    }
    if (error instanceof ZodError) {
      const issue = error.issues[0];
      return c.json(
        {
          error: {
            code: "invalid_request",
            message: `${issue?.path.join(".") || "body"}: ${issue?.message ?? "invalid"}`,
          },
        },
        400,
      );
    }
    const message = error instanceof Error ? error.message : String(error);
    if (message.includes("diagnostics_ingest_disabled")) {
      return c.json(
        {
          error: {
            code: "unavailable",
            message: "Log sharing is temporarily unavailable.",
          },
        },
        503,
      );
    }
    if (message.includes("diagnostics_rate_limited")) {
      return c.json(
        {
          error: {
            code: "rate_limited",
            message: "Too many reports today. Try again tomorrow.",
          },
        },
        429,
      );
    }
    console.error("diagnostics_request_failed", message);
    return c.json(
      {
        error: {
          code: "internal_error",
          message: "The report could not be stored.",
        },
      },
      500,
    );
  });

  app.use("*", async (c, next) => {
    await next();
    c.header("Cache-Control", "no-store");
    c.header("X-Content-Type-Options", "nosniff");
  });

  app.get("/", (c) => c.json({ service: "xerahs-diagnostics", ok: true }));

  app.get("/v1/installs/:installId/status", async (c) => {
    const installId = c.req.param("installId");
    if (!UUID.test(installId))
      throw new HttpError(400, "invalid_request", "installId must be a UUID.");
    return c.json(await store.status(installId.toLowerCase()));
  });

  app.post("/v1/reports", async (c) => {
    const report = rescrub(reportSchema.parse(await readBody(c.req.raw)));

    const ip = clientIp(c.req.raw.headers);
    const day = now().toISOString().slice(0, 10);
    const networkKey =
      networkSecret && ip
        ? createHmac("sha256", networkSecret)
            .update(`${day}\u001f${ip}`)
            .digest()
        : null;

    const deleteToken = randomBytes(32).toString("base64url");
    const result = await store.submit(report, sha256(deleteToken), networkKey);

    // A duplicate points at an earlier report; its delete token was issued then.
    return c.json(
      { ...result, deleteToken: result.duplicate ? null : deleteToken },
      result.duplicate ? 200 : 201,
    );
  });

  // Nightly Function Trigger (functions/diagnostics/neon.ts). Only Neon can
  // send x-neon-trigger-invocation-id: the Functions proxy strips client
  // x-neon-* headers, and the header must match the body's invocation_id.
  app.post("/v1/internal/maintenance", async (c) => {
    const parsed = await parseTriggerDelivery(c.req.raw);
    if (!parsed.ok) {
      return c.json(
        {
          error: { code: "unauthorized", message: "Trigger deliveries only." },
        },
        parsed.error === "invalid_body" ? 400 : 401,
      );
    }
    const result = await store.maintenance(
      `schedule:${parsed.invocation.trigger.name}`,
    );
    console.info("diagnostics_maintenance", result);
    return c.json(result);
  });

  app.delete("/v1/reports/:reportId", async (c) => {
    const reportId = c.req.param("reportId");
    const token = c.req.header("x-delete-token")?.trim();
    if (!UUID.test(reportId) || !token || token.length > 128) {
      throw new HttpError(
        400,
        "invalid_request",
        "A report id and X-Delete-Token are required.",
      );
    }
    if (!(await store.delete(reportId.toLowerCase(), sha256(token)))) {
      throw new HttpError(
        404,
        "not_found",
        "No report matches that id and token.",
      );
    }
    return c.body(null, 204);
  });

  return app;
}
