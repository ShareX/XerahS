import "server-only";

import { randomUUID } from "node:crypto";

import { rpc, serviceDatabaseClient } from "@/lib/database";
import { getServerEnv } from "@/lib/env";
import { ledgerKey, signLedgerPayload } from "@/lib/ledger/canonical";
import { FakeLedgerStore } from "@/lib/ledger/fake";
import { R2LedgerStore } from "@/lib/ledger/r2";
import type { LedgerOutboxEvent, LedgerStore } from "@/lib/ledger/types";

const fakeStore = new FakeLedgerStore();

function ledgerStore(): LedgerStore {
  const env = getServerEnv();
  if (
    env.LEDGER_USE_LOCAL_FAKE &&
    (env.APP_ENV === "development" || env.APP_ENV === "preview")
  )
    return fakeStore;
  if (
    !env.R2_LEDGER_ACCOUNT_ID ||
    !env.R2_LEDGER_BUCKET ||
    !env.R2_LEDGER_ACCESS_KEY_ID ||
    !env.R2_LEDGER_SECRET_ACCESS_KEY
  ) {
    throw new Error("R2 ledger configuration is incomplete.");
  }
  return new R2LedgerStore(
    env.R2_LEDGER_BUCKET,
    env.R2_LEDGER_ACCOUNT_ID,
    env.R2_LEDGER_ACCESS_KEY_ID,
    env.R2_LEDGER_SECRET_ACCESS_KEY,
  );
}

function signingSecret(version: string): string {
  const env = getServerEnv();
  if (env.LEDGER_HMAC_SECRETS_JSON) {
    let keys: unknown;
    try {
      keys = JSON.parse(env.LEDGER_HMAC_SECRETS_JSON);
    } catch {
      throw new Error("Ledger HMAC key ring is invalid JSON.");
    }
    if (typeof keys === "object" && keys !== null) {
      const secret = (keys as Record<string, unknown>)[version];
      if (typeof secret === "string" && secret.length >= 32) return secret;
    }
  }
  if (version === "v1" && env.LEDGER_HMAC_SECRET_V1)
    return env.LEDGER_HMAC_SECRET_V1;
  throw new Error(`Ledger HMAC key ${version} is unavailable.`);
}

interface ClaimedLedgerEvent {
  event_id: string;
  event_type: LedgerOutboxEvent["eventType"];
  canonical_payload: Record<string, unknown>;
  payload_sha256: string;
  hmac_key_version: number;
}

function errorCode(error: unknown): string {
  const value = error instanceof Error ? error.name : "UNKNOWN_ERROR";
  return value
    .toUpperCase()
    .replaceAll(/[^A-Z0-9_:-]/g, "_")
    .slice(0, 64);
}

export async function dispatchLedgerBatch(
  limit = 25,
): Promise<{ claimed: number; replicated: number; failed: number }> {
  const service = serviceDatabaseClient();
  const workerId = randomUUID();
  const claimed = await rpc<ClaimedLedgerEvent[]>(
    service,
    "claim_operations_ledger_events",
    {
      p_worker_id: workerId,
      p_limit: Math.min(limit, 100),
      p_lease_seconds: 300,
    },
  );
  const events: LedgerOutboxEvent[] = claimed.map((event) => ({
    eventId: event.event_id,
    eventType: event.event_type,
    occurredAt: String(event.canonical_payload.occurredAt),
    payload: event.canonical_payload,
    hmacKeyVersion: `v${event.hmac_key_version}`,
    payloadSha256: event.payload_sha256,
  }));
  const store = ledgerStore();
  let replicated = 0;
  let failed = 0;
  for (const event of events) {
    try {
      const body = signLedgerPayload(
        event.payload,
        event.hmacKeyVersion,
        signingSecret(event.hmacKeyVersion),
      );
      const key = ledgerKey(event.eventType, event.occurredAt, event.eventId);
      const object = await store.putIfAbsent(key, body);
      const integrity = JSON.parse(body) as {
        integrity: { signature: string };
      };
      await rpc(service, "acknowledge_operations_ledger_event", {
        p_event_id: event.eventId,
        p_worker_id: workerId,
        p_object_key: key,
        p_etag: object.etag,
        p_payload_sha256: event.payloadSha256,
        p_payload_hmac: `\\x${integrity.integrity.signature}`,
      });
      replicated += 1;
    } catch (error) {
      await rpc(service, "fail_operations_ledger_event", {
        p_event_id: event.eventId,
        p_worker_id: workerId,
        p_error_code: errorCode(error),
        p_retry_after_seconds: 60,
      });
      failed += 1;
    }
  }
  return { claimed: events.length, replicated, failed };
}

export async function attemptImmediateLedgerDispatch(limit = 1): Promise<void> {
  try {
    const result = await dispatchLedgerBatch(limit);
    if (result.failed > 0)
      console.error("ledger_immediate_dispatch_failed", {
        claimed: result.claimed,
        failed: result.failed,
      });
  } catch (error) {
    console.error("ledger_immediate_dispatch_unavailable", {
      errorCode: errorCode(error),
    });
  }
}
