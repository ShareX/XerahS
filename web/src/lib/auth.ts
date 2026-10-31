import "server-only";

import {
  createLocalJWKSet,
  jwtVerify,
  type JSONWebKeySet,
  type JWK,
} from "jose";
import { headers as requestHeaders } from "next/headers";
import { cache } from "react";

import {
  API_AUDIENCE,
  DESKTOP_ACCESS_TOKEN_SECONDS,
  DESKTOP_CLIENT_ID,
  auth,
  databasePool,
} from "@/lib/better-auth";
import type { DatabaseClient } from "@/lib/database";
import { getServerEnv } from "@/lib/env";
import { ApiError } from "@/lib/errors";
import { bearerAccessToken } from "@/lib/request";

export interface AuthenticatedUser {
  id: string;
  email: string;
  emailVerified: boolean;
  aal: "aal1" | "aal2";
  sessionId: string | null;
  /** When the session last passed a second factor or passkey. */
  authenticatedAt: Date | null;
  strongMethod: string | null;
}

interface AuthRequirements {
  strong?: boolean;
  recent?: boolean;
  verifiedEmail?: boolean;
}

const RECENT_STRONG_AUTH_MS = 10 * 60 * 1_000;

function signInRequired(): never {
  throw new ApiError(401, "authentication_required", "Sign in is required.");
}

interface SessionRow {
  user_id: string;
  aal: "aal1" | "aal2";
  strong_auth_at: Date | null;
  strong_auth_method: string | null;
  kind: "browser" | "desktop_grant";
  email: string | null;
  email_confirmed_at: Date | null;
}

async function loadSession(sessionId: string): Promise<SessionRow | null> {
  const { rows } = await databasePool().query<SessionRow>(
    `select s.user_id, s.aal, s.strong_auth_at, s.strong_auth_method, s.kind, u.email, u.email_confirmed_at
       from auth.sessions s join auth.users u on u.id = s.user_id
      where s.id = $1 and (u.banned_until is null or u.banned_until <= clock_timestamp())`,
    [sessionId],
  );
  return rows[0] ?? null;
}

// Public keys of the Better Auth JWT plugin, cached briefly so rotation is picked up.
let keySet:
  | { expires: number; get: ReturnType<typeof createLocalJWKSet> }
  | undefined;

async function signingKeys() {
  if (keySet && keySet.expires > Date.now()) return keySet.get;
  const { rows } = await databasePool().query<{
    id: string;
    publicKey: string;
    alg: string | null;
  }>(
    `select id, "publicKey", alg from better_auth.jwks where "expiresAt" is null or "expiresAt" > clock_timestamp()`,
  );
  const jwks: JSONWebKeySet = {
    keys: rows.map((row) => ({
      ...(JSON.parse(row.publicKey) as JWK),
      kid: row.id,
      alg: row.alg ?? "ES256",
    })),
  };
  keySet = {
    expires: Date.now() + 5 * 60 * 1_000,
    get: createLocalJWKSet(jwks),
  };
  return keySet.get;
}

/** Desktop access token from the OAuth server: ES256, our issuer and API audience. */
async function userFromBearer(token: string): Promise<AuthenticatedUser> {
  let payload: Record<string, unknown>;
  try {
    ({ payload } = await jwtVerify(token, await signingKeys(), {
      issuer: getServerEnv().APP_ORIGIN.replace(/\/$/, ""),
      audience: API_AUDIENCE,
      algorithms: ["ES256"],
      maxTokenAge: DESKTOP_ACCESS_TOKEN_SECONDS + 60,
    }));
  } catch {
    signInRequired();
  }
  const clientId = payload.azp ?? payload.client_id;
  const sessionId =
    typeof payload.session_id === "string" ? payload.session_id : null;
  if (
    typeof payload.sub !== "string" ||
    !sessionId ||
    !DESKTOP_CLIENT_ID ||
    clientId !== DESKTOP_CLIENT_ID
  ) {
    signInRequired();
  }
  // Checked on every request so revocation is immediate, not at token expiry.
  const session = await loadSession(sessionId);
  if (
    !session ||
    session.user_id !== payload.sub ||
    session.kind !== "desktop_grant" ||
    !session.email
  ) {
    signInRequired();
  }
  return {
    id: session.user_id,
    email: session.email,
    emailVerified: session.email_confirmed_at !== null,
    aal: session.aal,
    sessionId,
    authenticatedAt: session.strong_auth_at,
    strongMethod: session.strong_auth_method,
  };
}

async function userFromCookie(headers: Headers): Promise<AuthenticatedUser> {
  const current = await auth.api.getSession({ headers });
  if (!current) signInRequired();
  const session = await loadSession(current.session.id);
  if (!session || session.user_id !== current.user.id) signInRequired();
  return {
    id: current.user.id,
    email: current.user.email,
    emailVerified: current.user.emailVerified,
    aal: session.aal,
    sessionId: current.session.id,
    authenticatedAt: session.strong_auth_at,
    strongMethod: session.strong_auth_method,
  };
}

const resolved = new WeakMap<Request, Promise<AuthenticatedUser>>();

async function resolveUser(request?: Request): Promise<AuthenticatedUser> {
  if (request) {
    const existing = resolved.get(request);
    if (existing) return existing;
    const token = bearerAccessToken(request);
    const pending = token
      ? userFromBearer(token)
      : userFromCookie(request.headers);
    resolved.set(request, pending);
    pending.catch(() => resolved.delete(request));
    return pending;
  }
  return userFromCookie(await requestHeaders());
}

export async function requireAuthenticatedUser(
  request?: Request,
  requirements: AuthRequirements = {},
): Promise<AuthenticatedUser> {
  const user = await resolveUser(request);
  if (requirements.strong && user.aal !== "aal2") {
    throw new ApiError(
      403,
      "strong_auth_required",
      "Complete a strong-authentication challenge to continue.",
    );
  }
  if (
    requirements.recent &&
    (!user.authenticatedAt ||
      Date.now() - user.authenticatedAt.getTime() > RECENT_STRONG_AUTH_MS)
  ) {
    throw new ApiError(
      403,
      "strong_auth_required",
      "Recent strong authentication is required.",
    );
  }
  if (requirements.verifiedEmail && !user.emailVerified) {
    throw new ApiError(
      403,
      "email_verification_required",
      "Verify your email address to continue.",
    );
  }
  return user;
}

export const getOptionalAuthenticatedUser = cache(async function () {
  try {
    return await requireAuthenticatedUser();
  } catch (error) {
    if (
      error instanceof ApiError &&
      error.status === 401 &&
      error.code === "authentication_required"
    ) {
      return null;
    }
    throw error;
  }
});

/** Claims in the shape the XIP0085 SQL reads through auth.jwt(). */
export function databaseClaims(
  user: AuthenticatedUser,
): Record<string, unknown> {
  return {
    sub: user.id,
    role: "authenticated",
    email: user.email,
    aal: user.aal,
    session_id: user.sessionId,
    amr: user.authenticatedAt
      ? [
          {
            method:
              user.strongMethod === "mfa/webauthn" ? "mfa/webauthn" : "totp",
            timestamp: Math.floor(user.authenticatedAt.getTime() / 1_000),
          },
        ]
      : [{ method: "password", timestamp: Math.floor(Date.now() / 1_000) }],
  };
}

/** Database access as the signed-in user (cookie session or desktop token). */
export async function createUserDatabaseClient(
  request?: Request,
): Promise<DatabaseClient> {
  const user = await resolveUser(request);
  return { role: "authenticated", claims: databaseClaims(user) };
}
