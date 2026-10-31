// XerahS Cloud authentication: self-hosted Better Auth on Neon Postgres.
//
// Better Auth keeps its own tables in the `better_auth` schema. Triggers in
// db/cloud/migrations mirror users and sessions into auth.users and
// auth.sessions, which the XIP0085 SQL reads through auth.uid()/auth.jwt().
//
// Assurance levels match what the SQL expects:
//   aal1  password-only session
//   aal2  a second factor (TOTP, backup code) or a passkey was verified
// Sessions of users with two-factor enabled are only created after the
// second factor passes, so they start at aal2 (see the session trigger).
//
// Kept free of `@/` imports so the Better Auth CLI can load it.

import { oauthProvider } from "@better-auth/oauth-provider";
import { passkey } from "@better-auth/passkey";
import { betterAuth } from "better-auth";
import { createAuthMiddleware } from "better-auth/api";
import { nextCookies } from "better-auth/next-js";
import { jwt, twoFactor } from "better-auth/plugins";
import { PostgresDialect } from "kysely";
import pg from "pg";

import { sendEmail } from "./email";
import { verifyPasswordWithLegacy } from "./legacy-password";

const appOrigin = (process.env.APP_ORIGIN ?? "http://localhost:3000").replace(
  /\/$/,
  "",
);

/** Audience of desktop access tokens: the owner API. */
export const API_AUDIENCE = `${appOrigin}/api/v1`;

/** Only this public client (the XerahS desktop app) may use the OAuth server. */
export const DESKTOP_CLIENT_ID =
  process.env.XERAHS_DESKTOP_OAUTH_CLIENT_ID ?? "";

export const DESKTOP_ACCESS_TOKEN_SECONDS = 15 * 60;

let pool: pg.Pool | undefined;

/** One pool per server instance for Better Auth and application RPCs. */
export function databasePool(): pg.Pool {
  pool ??= new pg.Pool({
    connectionString: process.env.DATABASE_URL,
    max: 5,
    idleTimeoutMillis: 10_000,
  });
  return pool;
}

async function markSessionStrong(
  sessionId: string,
  method: string,
): Promise<void> {
  await databasePool().query(
    `update auth.sessions
        set aal = 'aal2', strong_auth_at = clock_timestamp(), strong_auth_method = $2, updated_at = clock_timestamp()
      where id = $1`,
    [sessionId, method],
  );
}

/**
 * A dedicated, revocable session row for one desktop authorization. Access
 * tokens carry its id as session_id, so signing out everywhere or deleting
 * the account (which delete auth.sessions rows) revokes the desktop too.
 */
async function createDesktopGrantSession(
  userId: string,
  approvingSessionId: string,
): Promise<string> {
  const { rows } = await databasePool().query<{ id: string; aal: string }>(
    `select id, aal from auth.sessions where id = $1 and user_id = $2`,
    [approvingSessionId, userId],
  );
  if (rows[0]?.aal !== "aal2") {
    throw new Error(
      "Desktop authorization requires a session verified with a second factor.",
    );
  }
  // The OAuth server asks for the reference more than once per approval
  // (consent, then the authorize step it re-enters), so this is idempotent
  // per approving browser session.
  const { rows: grant } = await databasePool().query<{ id: string }>(
    `insert into auth.sessions (id, user_id, aal, strong_auth_at, strong_auth_method, kind, parent_session_id)
     values (gen_random_uuid(), $1, 'aal2', clock_timestamp(), 'desktop_grant', 'desktop_grant', $2)
     on conflict (user_id, parent_session_id) where kind = 'desktop_grant'
       do update set updated_at = clock_timestamp()
     returning id`,
    [userId, approvingSessionId],
  );
  return grant[0]!.id;
}

async function desktopGrantClaims(
  userId: string | undefined,
  grantId: string | undefined,
) {
  if (!userId || !grantId)
    throw new Error("Desktop tokens need a user and an authorization grant.");
  const { rows } = await databasePool().query<{ strong_auth_at: Date }>(
    `select strong_auth_at from auth.sessions
      where id = $1 and user_id = $2 and kind = 'desktop_grant' and aal = 'aal2'`,
    [grantId, userId],
  );
  // A revoked grant must not mint new tokens (refresh included).
  if (!rows[0]) throw new Error("The desktop authorization was revoked.");
  return {
    role: "authenticated",
    aal: "aal2",
    session_id: grantId,
    amr: [
      {
        method: "mfa/desktop",
        timestamp: Math.floor(rows[0].strong_auth_at.getTime() / 1000),
      },
    ],
  };
}

export const auth = betterAuth({
  appName: "XerahS Cloud",
  baseURL: appOrigin,
  basePath: "/api/auth",
  secret: process.env.BETTER_AUTH_SECRET,
  trustedOrigins: [appOrigin],
  database: {
    dialect: new PostgresDialect({ pool: databasePool() }),
    type: "postgres",
    schemaName: "better_auth",
  },
  advanced: {
    database: { generateId: "uuid" },
    useSecureCookies: appOrigin.startsWith("https://"),
    cookiePrefix: "xerahs",
  },
  session: {
    expiresIn: 60 * 60 * 24 * 7,
    updateAge: 60 * 60 * 24,
    freshAge: 10 * 60,
  },
  emailAndPassword: {
    enabled: true,
    minPasswordLength: 12,
    maxPasswordLength: 256,
    requireEmailVerification: true,
    revokeSessionsOnPasswordReset: true,
    // Accounts imported from Supabase still have bcrypt hashes; new hashes stay scrypt.
    password: { verify: verifyPasswordWithLegacy },
    sendResetPassword: async ({ user, url }) => {
      await sendEmail({
        to: user.email,
        subject: "Reset your XerahS Cloud password",
        text: `Reset your XerahS Cloud password: ${url}\n\nIf you did not ask for this, ignore this email.`,
      });
    },
  },
  emailVerification: {
    sendOnSignUp: true,
    autoSignInAfterVerification: false,
    sendVerificationEmail: async ({ user, url }) => {
      await sendEmail({
        to: user.email,
        subject: "Verify your XerahS Cloud email address",
        text: `Confirm your email address for XerahS Cloud: ${url}\n\nIf you did not create an account, ignore this email.`,
      });
    },
  },
  rateLimit: {
    enabled: true,
    storage: "database",
    window: 60,
    max: 100,
    customRules: {
      "/sign-in/email": { window: 60, max: 10 },
      "/sign-up/email": { window: 60, max: 5 },
      "/two-factor/verify-totp": { window: 60, max: 10 },
      "/two-factor/verify-backup-code": { window: 60, max: 5 },
      "/request-password-reset": { window: 300, max: 5 },
    },
  },
  hooks: {
    after: createAuthMiddleware(async (ctx) => {
      const path = ctx.path;
      const session =
        ctx.context.newSession?.session ?? ctx.context.session?.session;
      if (!session) return;
      if (path === "/two-factor/verify-totp")
        await markSessionStrong(session.id, "totp");
      else if (path === "/two-factor/verify-backup-code")
        await markSessionStrong(session.id, "mfa");
      else if (path === "/passkey/verify-authentication")
        await markSessionStrong(session.id, "mfa/webauthn");
    }),
  },
  plugins: [
    twoFactor({
      issuer: "XerahS Cloud",
      skipVerificationOnEnable: false,
      backupCodeOptions: { amount: 10, length: 10 },
    }),
    passkey({
      rpID: new URL(appOrigin).hostname,
      rpName: "XerahS Cloud",
      origin: appOrigin,
    }),
    jwt({
      jwks: { keyPairConfig: { alg: "ES256" } },
      jwt: { issuer: appOrigin, audience: API_AUDIENCE },
    }),
    oauthProvider({
      loginPage: "/auth",
      consentPage: "/oauth/consent",
      scopes: ["openid", "profile", "email", "offline_access"],
      validAudiences: [API_AUDIENCE],
      // The owner API is the only resource; the desktop client is linked to
      // it by scripts/register-desktop-oauth-client.ts (per-client
      // enforcement stays on).
      resources: [
        {
          identifier: API_AUDIENCE,
          name: "XerahS Cloud owner API",
          accessTokenTtl: DESKTOP_ACCESS_TOKEN_SECONDS,
          signingAlgorithm: "ES256",
          allowedScopes: ["openid", "profile", "email", "offline_access"],
        },
      ],
      accessTokenExpiresIn: DESKTOP_ACCESS_TOKEN_SECONDS,
      refreshTokenExpiresIn: 60 * 60 * 24 * 30,
      allowDynamicClientRegistration: false,
      postLogin: {
        page: "/oauth/consent",
        shouldRedirect: () => false,
        consentReferenceId: async ({ user, session }) =>
          createDesktopGrantSession(user.id, session.id),
      },
      customAccessTokenClaims: async ({ user, referenceId }) =>
        desktopGrantClaims(user?.id, referenceId),
    }),
    nextCookies(),
  ],
});

export type Auth = typeof auth;
