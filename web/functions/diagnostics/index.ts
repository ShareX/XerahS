// XerahS diagnostics ingest, deployed as the Neon Function `diagnostics`.
// Receives reports from the desktop Debug tab and stores them in the
// `diagnostics` schema of the same branch.

import { attachDatabasePool } from "@neon/functions";
import pg from "pg";

import {
  createApp,
  type DiagnosticsStore,
  type InstallStatus,
  type SubmitResult,
} from "./app";

const pool = new pg.Pool({
  connectionString: process.env.DATABASE_URL,
  max: 5,
});
attachDatabasePool(pool);

/** Runs `sql` as diagnostics_ingest so this Function can only use the ingest API. */
async function asIngestRole<T>(sql: string, params: unknown[]): Promise<T> {
  const client = await pool.connect();
  try {
    await client.query("begin");
    await client.query("set local role diagnostics_ingest");
    const { rows } = await client.query<{ result: T }>(sql, params);
    await client.query("commit");
    return rows[0]?.result as T;
  } catch (error) {
    await client.query("rollback").catch(() => undefined);
    throw error;
  } finally {
    client.release();
  }
}

const store: DiagnosticsStore = {
  submit: (report, deleteTokenHash, networkKey) =>
    asIngestRole<SubmitResult>(
      "select diagnostics.submit_report($1::jsonb, $2, $3) as result",
      [JSON.stringify(report), deleteTokenHash, networkKey],
    ),
  status: (installId) =>
    asIngestRole<InstallStatus>(
      "select diagnostics.install_status($1::uuid) as result",
      [installId],
    ),
  delete: (reportId, deleteTokenHash) =>
    asIngestRole<boolean>(
      "select diagnostics.delete_report_with_token($1::uuid, $2) as result",
      [reportId, deleteTokenHash],
    ),
  maintenance: (trigger) =>
    asIngestRole<unknown>("select diagnostics.run_maintenance($1) as result", [
      trigger,
    ]),
};

export default createApp({
  store,
  networkSecret: process.env.DIAGNOSTICS_NETWORK_SECRET,
});
