// Registers (or updates) the XerahS desktop app as the OAuth client of the
// Better Auth OAuth server: a public client using authorization code + PKCE,
// consent required, redirecting to the web relay that hands the code to
// xerahs://oauth/callback.
//
//   node scripts/register-desktop-oauth-client.ts [--env .env.local]
//
// Needs DATABASE_URL_UNPOOLED (or DATABASE_URL), APP_ORIGIN and
// XERAHS_DESKTOP_OAUTH_CLIENT_ID. Safe to re-run.

import { existsSync, readFileSync } from "node:fs";
import process from "node:process";

import pg from "pg";

function loadEnvFile(path: string): void {
  if (!existsSync(path)) return;
  for (const line of readFileSync(path, "utf8").split(/\r?\n/)) {
    const match = /^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/.exec(line);
    if (!match?.[1] || process.env[match[1]] !== undefined) continue;
    process.env[match[1]] = (match[2] ?? "").replace(/^(['"])(.*)\1$/, "$2");
  }
}

const envIndex = process.argv.indexOf("--env");
loadEnvFile(
  envIndex >= 0 ? (process.argv[envIndex + 1] ?? ".env.local") : ".env.local",
);

const connectionString =
  process.env.DATABASE_URL_UNPOOLED ?? process.env.DATABASE_URL;
const appOrigin = process.env.APP_ORIGIN?.replace(/\/$/, "");
const clientId = process.env.XERAHS_DESKTOP_OAUTH_CLIENT_ID;
if (!connectionString || !appOrigin || !clientId) {
  console.error(
    "DATABASE_URL(_UNPOOLED), APP_ORIGIN and XERAHS_DESKTOP_OAUTH_CLIENT_ID are required.",
  );
  process.exit(2);
}

// Same rule as src/lib/oauth-validation.ts desktopOAuthRedirectUris().
const primary = new URL("/auth/desktop/callback", appOrigin).href;
const redirectUris =
  primary === "https://cloud.xerahs.com/auth/desktop/callback"
    ? [primary, "https://staging.xerahs.com/auth/desktop/callback"]
    : [primary];

const client = new pg.Client({
  connectionString: connectionString.replace(
    /sslmode=require\b/,
    "sslmode=verify-full",
  ),
});
await client.connect();
try {
  const { rows } = await client.query<{ clientId: string; inserted: boolean }>(
    `insert into better_auth."oauthClient" (
       "clientId", "clientSecret", name, "redirectUris", "tokenEndpointAuthMethod", "grantTypes",
       "responseTypes", "requirePKCE", "skipConsent", disabled, scopes, "applicationType",
       "subjectType", "createdAt", "updatedAt")
     values ($1, null, 'XerahS desktop', $2::jsonb, 'none', '["authorization_code","refresh_token"]'::jsonb,
       '["code"]'::jsonb, true, false, false, '["openid","profile","email","offline_access"]'::jsonb, 'native',
       'public', clock_timestamp(), clock_timestamp())
     on conflict ("clientId") do update set
       "clientSecret" = null,
       "redirectUris" = excluded."redirectUris",
       "tokenEndpointAuthMethod" = 'none',
       "grantTypes" = excluded."grantTypes",
       "responseTypes" = excluded."responseTypes",
       "requirePKCE" = true,
       "skipConsent" = false,
       disabled = false,
       scopes = excluded.scopes,
       "updatedAt" = clock_timestamp()
     returning "clientId", (xmax = 0) as inserted`,
    [clientId, JSON.stringify(redirectUris)],
  );
  // The owner API resource (also seeded from src/lib/better-auth.ts) and the
  // client's link to it; tokens for any other audience are refused.
  const audience = `${appOrigin}/api/v1`;
  await client.query(
    `insert into better_auth."oauthResource" (identifier, name, "accessTokenTtl", "signingAlgorithm", "allowedScopes", disabled, "createdAt", "updatedAt")
     values ($1, 'XerahS Cloud owner API', 900, 'ES256', '["openid","profile","email","offline_access"]'::jsonb, false, clock_timestamp(), clock_timestamp())
     on conflict (identifier) do nothing`,
    [audience],
  );
  await client.query(
    `insert into better_auth."oauthClientResource" ("clientId", "resourceId", "createdAt")
     values ($1, $2, clock_timestamp())
     on conflict ("clientId", "resourceId") do nothing`,
    [clientId, audience],
  );
  console.log(
    `${rows[0]?.inserted ? "Registered" : "Updated"} OAuth client ${rows[0]?.clientId} -> ${redirectUris.join(", ")}; linked to ${audience}`,
  );
} finally {
  await client.end();
}
