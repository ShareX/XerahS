import { ApiError } from "@/lib/errors";

// The desktop app is the only OAuth client. Consent is shown for exactly its
// authorization-code + PKCE request, for its relay redirect, its scopes and
// the owner API audience; anything else is refused before Better Auth sees it.

export const desktopOAuthScopes = [
  "openid",
  "email",
  "profile",
  "offline_access",
] as const;

export interface DesktopAuthorizationExpectation {
  clientId: string;
  redirectUris: readonly string[];
  audience: string;
  now?: number;
}

export interface DesktopAuthorizationRequest {
  clientId: string;
  redirectUri: string;
  scopes: string[];
  /** The signed query to hand back to /api/auth/oauth2/consent unchanged. */
  oauthQuery: string;
}

const legacyDesktopRedirectUri =
  "https://staging.xerahs.com/auth/desktop/callback";
const canonicalDesktopRedirectUri =
  "https://cloud.xerahs.com/auth/desktop/callback";
const MAX_QUERY_LENGTH = 8_192;

export function desktopOAuthRedirectUris(appOrigin: string): readonly string[] {
  const primary = new URL("/auth/desktop/callback", appOrigin).href;
  return primary === canonicalDesktopRedirectUri
    ? [primary, legacyDesktopRedirectUri]
    : [primary];
}

function invalidAuthorization(): never {
  throw new ApiError(
    403,
    "forbidden",
    "The OAuth authorization request is not permitted.",
  );
}

function exactScopes(scope: string): boolean {
  const values = scope.trim().split(/\s+/).filter(Boolean);
  const requested = new Set(values);
  return (
    values.length === desktopOAuthScopes.length &&
    requested.size === desktopOAuthScopes.length &&
    desktopOAuthScopes.every((value) => requested.has(value))
  );
}

function exactRedirectUri(actual: string, expected: string): boolean {
  try {
    const actualUrl = new URL(actual);
    const expectedUrl = new URL(expected);
    return (
      (actualUrl.protocol === "https:" || actualUrl.hostname === "localhost") &&
      actualUrl.href === expectedUrl.href &&
      !actualUrl.username &&
      !actualUrl.password &&
      !actualUrl.search &&
      !actualUrl.hash
    );
  } catch {
    return false;
  }
}

function single(query: URLSearchParams, name: string): string {
  const values = query.getAll(name);
  if (values.length !== 1 || !values[0]) invalidAuthorization();
  return values[0];
}

/**
 * Validates the signed authorization query Better Auth passes to the consent
 * page. The signature itself is checked again by /oauth2/consent.
 */
export function parseDesktopAuthorizationQuery(
  query: URLSearchParams,
  expected: DesktopAuthorizationExpectation,
): DesktopAuthorizationRequest {
  const oauthQuery = query.toString();
  if (oauthQuery.length > MAX_QUERY_LENGTH) invalidAuthorization();

  const clientId = single(query, "client_id");
  const redirectUri = single(query, "redirect_uri");
  const scope = single(query, "scope");
  const exp = Number(single(query, "exp"));
  single(query, "sig");
  single(query, "state");
  if (
    !expected.clientId ||
    clientId !== expected.clientId ||
    single(query, "response_type") !== "code" ||
    single(query, "code_challenge_method") !== "S256" ||
    !/^[A-Za-z0-9_-]{43,128}$/.test(single(query, "code_challenge")) ||
    single(query, "resource") !== expected.audience ||
    !single(query, "prompt").split(/\s+/).includes("consent") ||
    !expected.redirectUris.some((value) =>
      exactRedirectUri(redirectUri, value),
    ) ||
    !exactScopes(scope) ||
    !Number.isFinite(exp) ||
    exp * 1_000 <= (expected.now ?? Date.now())
  ) {
    invalidAuthorization();
  }
  return {
    clientId,
    redirectUri,
    scopes: scope.trim().split(/\s+/),
    oauthQuery,
  };
}

export function assertDesktopOAuthRedirect(
  redirectUrl: string,
  expectedRedirectUris: readonly string[],
  issuer?: string,
): URL {
  let actual: URL;
  try {
    actual = new URL(redirectUrl);
  } catch {
    return invalidAuthorization();
  }
  if (
    (actual.protocol !== "https:" && actual.hostname !== "localhost") ||
    !expectedRedirectUris.some((value) => {
      try {
        const expected = new URL(value);
        return (
          actual.origin === expected.origin &&
          actual.pathname === expected.pathname
        );
      } catch {
        return false;
      }
    }) ||
    actual.username ||
    actual.password ||
    actual.hash
  ) {
    return invalidAuthorization();
  }
  const state = actual.searchParams.getAll("state");
  const code = actual.searchParams.getAll("code");
  const error = actual.searchParams.getAll("error");
  const errorDescription = actual.searchParams.getAll("error_description");
  const iss = actual.searchParams.getAll("iss");
  // RFC 9207: the authorization server names itself in the response.
  const allowedKeys = new Set([
    "state",
    "code",
    "error",
    "error_description",
    "iss",
  ]);
  if (
    [...actual.searchParams.keys()].some((key) => !allowedKeys.has(key)) ||
    state.length !== 1 ||
    state[0]?.length === 0 ||
    (state[0]?.length ?? 0) > 1024 ||
    (code.length === 1) === (error.length === 1) ||
    code.length > 1 ||
    error.length > 1 ||
    (code[0]?.length ?? error[0]?.length ?? 0) === 0 ||
    (code[0]?.length ?? 0) > 4096 ||
    (error[0] !== undefined && !/^[A-Za-z0-9_]{1,128}$/.test(error[0])) ||
    (code.length === 1 && errorDescription.length !== 0) ||
    errorDescription.length > 1 ||
    (errorDescription[0]?.length ?? 0) > 512 ||
    iss.length > 1 ||
    (iss.length === 1 && issuer !== undefined && iss[0] !== issuer)
  ) {
    return invalidAuthorization();
  }
  return actual;
}
