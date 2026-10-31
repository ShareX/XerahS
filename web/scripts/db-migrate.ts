// Applies <dir>/*.sql in name order, each once, each in its own
// transaction, and records them in public.schema_migrations.
//
//   node scripts/db-migrate.ts --dir db/cloud/migrations --env .env.local [--status]
//   node scripts/db-migrate.ts --dir db/diagnostics/migrations --env functions/diagnostics/.env.local
//
// Uses DATABASE_URL_UNPOOLED (or DATABASE_URL) from the environment, then
// the --env file. Refuses to continue if an applied file has changed.

import { createHash } from "node:crypto";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, resolve } from "node:path";
import process from "node:process";

import pg from "pg";

function argument(name: string): string | undefined {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : undefined;
}

const MIGRATIONS_DIR = resolve(
  argument("--dir") ?? "db/diagnostics/migrations",
);

function loadEnvFile(path: string): void {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, "utf8").split(/\r?\n/)) {
    const match = /^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/.exec(line);
    if (!match?.[1] || process.env[match[1]] !== undefined) continue;
    process.env[match[1]] = (match[2] ?? "").replace(/^(['"])(.*)\1$/, "$2");
  }
}

loadEnvFile(argument("--env") ?? ".env.local");
const connectionString =
  process.env.DATABASE_URL_UNPOOLED ?? process.env.DATABASE_URL;
if (!connectionString) {
  console.error(
    "DATABASE_URL_UNPOOLED or DATABASE_URL is required (run `neon env pull`).",
  );
  process.exit(2);
}

const statusOnly = process.argv.includes("--status");
const files = readdirSync(MIGRATIONS_DIR)
  .filter((name) => /^\d+_[a-z0-9_]+\.sql$/.test(name))
  .sort();

const client = new pg.Client({
  connectionString: connectionString.replace(
    /sslmode=require\b/,
    "sslmode=verify-full",
  ),
});
await client.connect();
try {
  await client.query(`
    create table if not exists public.schema_migrations (
      name text primary key,
      sha256 text not null,
      applied_at timestamptz not null default clock_timestamp()
    )`);
  // One migrator at a time.
  await client.query(
    "select pg_advisory_lock(hashtext('xerahs.schema_migrations'))",
  );

  const { rows } = await client.query<{ name: string; sha256: string }>(
    "select name, sha256 from public.schema_migrations",
  );
  const applied = new Map(rows.map((row) => [row.name, row.sha256]));

  for (const name of files) {
    const sql = readFileSync(join(MIGRATIONS_DIR, name), "utf8");
    const sha256 = createHash("sha256").update(sql).digest("hex");
    const recorded = applied.get(name);
    if (recorded) {
      if (recorded !== sha256) {
        throw new Error(
          `${name} changed after it was applied. Add a new migration instead of editing it.`,
        );
      }
      console.log(`applied  ${name}`);
      continue;
    }
    if (statusOnly) {
      console.log(`pending  ${name}`);
      continue;
    }
    await client.query("begin");
    try {
      await client.query(sql);
      await client.query(
        "insert into public.schema_migrations (name, sha256) values ($1, $2)",
        [name, sha256],
      );
      await client.query("commit");
      console.log(`applying ${name} ... done`);
    } catch (error) {
      await client.query("rollback");
      throw new Error(
        `${name} failed: ${error instanceof Error ? error.message : String(error)}`,
      );
    }
  }
} finally {
  await client.end();
}
