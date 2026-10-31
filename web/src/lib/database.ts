import "server-only";

import type { PoolClient } from "pg";

import { databasePool } from "@/lib/better-auth";
import { ApiError, mapDatabaseError } from "@/lib/errors";

export interface AccountSummary {
  slug: string;
  timeZone: string;
  strongAuth: boolean;
  trialStatus: "not_started" | "trial_pending" | "active" | "expired";
  trialEndsAt: string | null;
  subscriptionStatus: string | null;
  paidThrough: string | null;
  canPublish: boolean;
  disputeSuspended: boolean;
}

export interface GalleryItem {
  id: string;
  clientItemId: string;
  url: string;
  thumbnailUrl: string | null;
  kind: "screenshot" | "screencast";
  fileName: string;
  title: string;
  capturedAt: string;
  publishedAt: string;
  host: string | null;
  contentType: string | null;
}

/**
 * Who a database call runs as. The XIP0085 SQL was written for PostgREST, so
 * each call does what PostgREST does: switch to the role, publish the
 * claims for auth.uid()/auth.jwt(), then run the function under RLS.
 */
export interface DatabaseClient {
  readonly role: "authenticated" | "service_role";
  readonly claims: Record<string, unknown>;
}

export function serviceDatabaseClient(): DatabaseClient {
  return { role: "service_role", claims: { role: "service_role" } };
}

async function withClient<T>(
  client: DatabaseClient,
  work: (connection: PoolClient) => Promise<T>,
): Promise<T> {
  const connection = await databasePool().connect();
  try {
    await connection.query("begin");
    await connection.query(
      `set local role ${client.role === "service_role" ? "service_role" : "authenticated"}`,
    );
    await connection.query(
      "select set_config('request.jwt.claims', $1, true)",
      [JSON.stringify(client.claims)],
    );
    const result = await work(connection);
    await connection.query("commit");
    return result;
  } catch (error) {
    await connection.query("rollback").catch(() => undefined);
    throw error;
  } finally {
    connection.release();
  }
}

const IDENTIFIER = /^[a-z_][a-z0-9_]*$/;
const returnsSet = new Map<string, boolean>();

async function functionReturnsSet(
  connection: PoolClient,
  name: string,
): Promise<boolean> {
  const cached = returnsSet.get(name);
  if (cached !== undefined) return cached;
  const { rows } = await connection.query<{ retset: boolean }>(
    "select bool_or(proretset) as retset from pg_catalog.pg_proc where proname = $1 and pronamespace = 'public'::regnamespace",
    [name],
  );
  const value = rows[0]?.retset ?? false;
  returnsSet.set(name, value);
  return value;
}

/** Calls public.<name>(arg => value, ...) like PostgREST's /rpc endpoint. */
export async function rpc<T>(
  client: DatabaseClient,
  name: string,
  args: Record<string, unknown> = {},
): Promise<T> {
  if (!IDENTIFIER.test(name)) throw new Error(`Invalid function name: ${name}`);
  const entries = Object.entries(args).filter(
    ([, value]) => value !== undefined,
  );
  for (const [key] of entries) {
    if (!IDENTIFIER.test(key)) throw new Error(`Invalid argument name: ${key}`);
  }
  const call = `public.${name}(${entries.map(([key], index) => `${key} => $${index + 1}`).join(", ")})`;
  const values = entries.map(([, value]) =>
    value !== null && typeof value === "object" && !Array.isArray(value)
      ? JSON.stringify(value)
      : value,
  );

  try {
    return await withClient(client, async (connection) => {
      if (await functionReturnsSet(connection, name)) {
        const { rows } = await connection.query(
          `select * from ${call}`,
          values,
        );
        return rows as T;
      }
      const { rows } = await connection.query<{ result: T }>(
        `select ${call} as result`,
        values,
      );
      return rows[0]?.result as T;
    });
  } catch (error) {
    throw mapDatabaseError(error);
  }
}

/** Runs a read under the client's role and claims, so RLS applies. */
export async function query<T>(
  client: DatabaseClient,
  sql: string,
  values: unknown[] = [],
): Promise<T[]> {
  try {
    return await withClient(
      client,
      async (connection) => (await connection.query(sql, values)).rows as T[],
    );
  } catch (error) {
    throw mapDatabaseError(error);
  }
}

function firstRow<T>(data: unknown): T {
  const value = Array.isArray(data) ? data[0] : data;
  if (!value || typeof value !== "object")
    throw new ApiError(
      500,
      "internal_error",
      "The database returned an invalid result.",
    );
  return value as T;
}

export async function getAccountSummary(
  client: DatabaseClient,
): Promise<AccountSummary> {
  return firstRow<AccountSummary>(await rpc(client, "get_my_account_summary"));
}
