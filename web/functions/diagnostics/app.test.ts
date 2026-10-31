import { gzipSync } from "node:zlib";

import { describe, expect, it } from "vitest";

import {
  createApp,
  MAX_INFLATED_BYTES,
  type DiagnosticsStore,
  type SubmitResult,
} from "./app";
import type { DiagnosticsReport } from "./schema";

function report(overrides: Record<string, unknown> = {}) {
  return {
    schemaVersion: 1,
    redactionVersion: 1,
    trigger: "manual",
    installId: "11111111-1111-4111-8111-111111111111",
    clientReportId: "22222222-2222-4222-8222-222222222222",
    comment:
      "Crashes when I press PrintScreen. Contact me at dream@example.com",
    app: {
      version: "0.32.0",
      buildFlavor: "Release",
      dotnetVersion: "10.0.12",
    },
    os: { family: "windows", architecture: "X64", processArchitecture: "X64" },
    displays: [
      {
        ordinal: 0,
        width: 3840,
        height: 2160,
        scale: 1.5,
        rotation: 0,
        isHdr: false,
      },
    ],
    window: {
      kind: "24h",
      start: "2026-09-28T14:00:00Z",
      end: "2026-09-29T14:00:00Z",
    },
    events: [
      {
        kind: "error_line",
        messageTemplate: "saved to C:\\Users\\Dream\\Pictures",
        occurrences: 2,
      },
    ],
    logs: [
      {
        fileName: "XerahS-20260929.log",
        content:
          "2026-09-29 13:00:00.000 - Loaded C:\\Users\\Dream\\Documents\\x.json",
      },
    ],
    ...overrides,
  };
}

const maintenanceRuns: string[] = [];

function fakeStore(result: Partial<SubmitResult> = {}) {
  const calls: Array<{ report: DiagnosticsReport; networkKey: Buffer | null }> =
    [];
  const store: DiagnosticsStore = {
    submit: async (r, _hash, networkKey) => {
      calls.push({ report: r, networkKey });
      return {
        reportId: "33333333-3333-4333-8333-333333333333",
        receivedAt: "2026-09-29T14:01:00Z",
        duplicate: false,
        reason: null,
        primarySignatureId: 7,
        ...result,
      };
    },
    status: async () => ({
      lastLogAt: "2026-09-29T14:00:00Z",
      lastReportId: null,
      lastReceivedAt: null,
      reportCount: 1,
    }),
    delete: async (_id, hash) => hash.length === 32,
    maintenance: async (trigger) => {
      maintenanceRuns.push(trigger);
      return { reportsDeleted: 0 };
    },
  };
  return { store, calls };
}

const now = () => new Date("2026-09-29T15:00:00Z");

function post(
  body: unknown,
  init: { gzip?: boolean; headers?: Record<string, string> } = {},
) {
  const json = Buffer.from(JSON.stringify(body));
  return new Request("https://fn.example/v1/reports", {
    method: "POST",
    headers: {
      "content-type": "application/json",
      ...(init.gzip ? { "content-encoding": "gzip" } : {}),
      "x-forwarded-for": "203.0.113.9, 10.0.0.1",
      ...init.headers,
    },
    body: init.gzip ? gzipSync(json) : json,
  });
}

describe("POST /v1/reports", () => {
  it("accepts a gzip report, rescrubs it, and returns a delete token", async () => {
    const { store, calls } = fakeStore();
    const app = createApp({ store, networkSecret: "s".repeat(32), now });
    const response = await app.request(post(report(), { gzip: true }));
    expect(response.status).toBe(201);
    const body = (await response.json()) as SubmitResult & {
      deleteToken: string;
    };
    expect(body.reportId).toBe("33333333-3333-4333-8333-333333333333");
    expect(body.deleteToken).toMatch(/^[A-Za-z0-9_-]{43}$/);

    const stored = calls[0]?.report;
    expect(JSON.stringify(stored)).not.toMatch(/dream/i);
    expect(stored?.comment).toContain("<email>");
    expect(stored?.logs[0]?.content).toContain("C:\\Users\\<user>\\Documents");
    expect(calls[0]?.networkKey).toHaveLength(32);
  });

  it("returns the earlier report for duplicates without a new token", async () => {
    const { store } = fakeStore({
      duplicate: true,
      reason: "no_new_entries",
      reportId: "44444444-4444-4444-8444-444444444444",
    });
    const app = createApp({ store, networkSecret: undefined, now });
    const response = await app.request(post(report()));
    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({
      duplicate: true,
      reason: "no_new_entries",
      reportId: "44444444-4444-4444-8444-444444444444",
      deleteToken: null,
    });
  });

  it("rejects month-long windows and other invalid reports", async () => {
    const app = createApp({
      store: fakeStore().store,
      networkSecret: undefined,
      now,
    });
    const month = await app.request(
      post(
        report({
          window: {
            kind: "30d",
            start: "2026-08-30T14:00:00Z",
            end: "2026-09-29T14:00:00Z",
          },
        }),
      ),
    );
    expect(month.status).toBe(400);
    const tooLong = await app.request(
      post(
        report({
          window: {
            kind: "24h",
            start: "2026-09-20T14:00:00Z",
            end: "2026-09-29T14:00:00Z",
          },
        }),
      ),
    );
    expect(tooLong.status).toBe(400);
    expect(
      ((await tooLong.json()) as { error: { message: string } }).error.message,
    ).toContain("longer than 24h");
    const badId = await app.request(post(report({ installId: "not-a-uuid" })));
    expect(badId.status).toBe(400);
  });

  it("refuses gzip bombs and non-JSON bodies", async () => {
    const app = createApp({
      store: fakeStore().store,
      networkSecret: undefined,
      now,
    });
    const bomb = new Request("https://fn.example/v1/reports", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "content-encoding": "gzip",
      },
      body: gzipSync(Buffer.alloc(MAX_INFLATED_BYTES + 1, 32)),
    });
    expect((await app.request(bomb)).status).toBe(413);
    const text = new Request("https://fn.example/v1/reports", {
      method: "POST",
      headers: { "content-type": "text/plain" },
      body: "hi",
    });
    expect((await app.request(text)).status).toBe(415);
  });

  it("maps database refusals to 503 and 429", async () => {
    const failing = (message: string): DiagnosticsStore => ({
      ...fakeStore().store,
      submit: async () => {
        throw new Error(message);
      },
    });
    const disabled = createApp({
      store: failing("diagnostics_ingest_disabled"),
      networkSecret: undefined,
      now,
    });
    expect((await disabled.request(post(report()))).status).toBe(503);
    const limited = createApp({
      store: failing("diagnostics_rate_limited"),
      networkSecret: undefined,
      now,
    });
    expect((await limited.request(post(report()))).status).toBe(429);
  });
});

describe("status and delete", () => {
  const app = createApp({
    store: fakeStore().store,
    networkSecret: undefined,
    now,
  });

  it("reports install status", async () => {
    const response = await app.request(
      "/v1/installs/11111111-1111-4111-8111-111111111111/status",
    );
    expect(response.status).toBe(200);
    expect(await response.json()).toMatchObject({
      lastLogAt: "2026-09-29T14:00:00Z",
      reportCount: 1,
    });
    expect((await app.request("/v1/installs/nope/status")).status).toBe(400);
  });

  it("deletes with a token", async () => {
    const ok = await app.request(
      "/v1/reports/33333333-3333-4333-8333-333333333333",
      {
        method: "DELETE",
        headers: { "x-delete-token": "token" },
      },
    );
    expect(ok.status).toBe(204);
    const missing = await app.request(
      "/v1/reports/33333333-3333-4333-8333-333333333333",
      { method: "DELETE" },
    );
    expect(missing.status).toBe(400);
  });
});

describe("POST /v1/internal/maintenance", () => {
  const app = createApp({
    store: fakeStore().store,
    networkSecret: undefined,
    now,
  });
  const delivery = {
    version: 1,
    invocation_id: "inv-123",
    trigger: {
      type: "schedule",
      id: "trigger-1",
      name: "diagnostics-maintenance",
    },
    data: { scheduled_at: "2026-09-30T18:17:00Z" },
  };

  it("refuses anything that is not a Neon trigger delivery", async () => {
    const response = await app.request("/v1/internal/maintenance", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(delivery),
    });
    expect(response.status).toBe(401);
    const mismatch = await app.request("/v1/internal/maintenance", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-neon-trigger-invocation-id": "other",
      },
      body: JSON.stringify(delivery),
    });
    expect(mismatch.status).toBe(401);
    expect(maintenanceRuns).toHaveLength(0);
  });

  it("runs maintenance for a trigger delivery", async () => {
    const response = await app.request("/v1/internal/maintenance", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-neon-trigger-invocation-id": "inv-123",
      },
      body: JSON.stringify(delivery),
    });
    expect(response.status).toBe(200);
    expect(maintenanceRuns).toEqual(["schedule:diagnostics-maintenance"]);
  });
});
