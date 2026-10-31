import { describe, expect, it } from "vitest";

import {
  assertDesktopOAuthRedirect,
  desktopOAuthRedirectUris,
  parseDesktopAuthorizationQuery,
} from "@/lib/oauth-validation";

const now = Date.parse("2026-10-01T00:00:00Z");
const expected = {
  clientId: "xerahs-desktop",
  redirectUris: [
    "https://cloud.xerahs.com/auth/desktop/callback",
    "https://staging.xerahs.com/auth/desktop/callback",
  ],
  audience: "https://cloud.xerahs.com/api/v1",
  now,
};
const issuer = "https://cloud.xerahs.com";

function query(changes: Record<string, string | null> = {}): URLSearchParams {
  const values: Record<string, string> = {
    response_type: "code",
    client_id: expected.clientId,
    redirect_uri: expected.redirectUris[0]!,
    scope: "openid email profile offline_access",
    state: "state-value",
    code_challenge: "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
    code_challenge_method: "S256",
    resource: expected.audience,
    prompt: "consent",
    exp: String(now / 1_000 + 300),
    sig: "signature",
  };
  for (const [key, value] of Object.entries(changes)) {
    if (value === null) delete values[key];
    else values[key] = value;
  }
  return new URLSearchParams(values);
}

describe("desktop OAuth consent validation", () => {
  it("accepts only the desktop client's exact authorization request", () => {
    const parsed = parseDesktopAuthorizationQuery(query(), expected);
    expect(parsed.clientId).toBe("xerahs-desktop");
    expect(parsed.oauthQuery).toContain("sig=signature");
    expect(() =>
      parseDesktopAuthorizationQuery(
        query({ scope: "profile openid offline_access email" }),
        expected,
      ),
    ).not.toThrow();
    expect(() =>
      parseDesktopAuthorizationQuery(
        query({ redirect_uri: expected.redirectUris[1]! }),
        expected,
      ),
    ).not.toThrow();
  });

  it.each<Record<string, string | null>>([
    { client_id: "other" },
    { redirect_uri: "https://evil.example/callback" },
    { redirect_uri: `${expected.redirectUris[0]}?next=evil` },
    { scope: "openid email profile" },
    { scope: "openid email profile offline_access phone" },
    { scope: "openid email email offline_access" },
    { response_type: "token" },
    { code_challenge_method: "plain" },
    { code_challenge: null },
    { resource: "https://evil.example/api" },
    { prompt: "none" },
    { sig: null },
    { exp: String(now / 1_000 - 1) },
  ])("rejects %o", (change) => {
    expect(() =>
      parseDesktopAuthorizationQuery(query(change), expected),
    ).toThrowError("not permitted");
  });

  it("rejects duplicated parameters", () => {
    const params = query();
    params.append("client_id", "other");
    expect(() => parseDesktopAuthorizationQuery(params, expected)).toThrowError(
      "not permitted",
    );
  });

  it("restricts OAuth redirects to the exact desktop relay", () => {
    expect(desktopOAuthRedirectUris("https://cloud.xerahs.com")).toEqual(
      expected.redirectUris,
    );
    expect(desktopOAuthRedirectUris("http://localhost:3000")).toEqual([
      "http://localhost:3000/auth/desktop/callback",
    ]);
    expect(
      assertDesktopOAuthRedirect(
        `${expected.redirectUris[0]}?code=one&state=two&iss=${encodeURIComponent(issuer)}`,
        expected.redirectUris,
        issuer,
      ).pathname,
    ).toBe("/auth/desktop/callback");
    expect(
      assertDesktopOAuthRedirect(
        `${expected.redirectUris[1]}?error=access_denied&error_description=Denied&state=two`,
        expected.redirectUris,
        issuer,
      ).searchParams.get("error"),
    ).toBe("access_denied");
    for (const bad of [
      "https://evil.example/callback?code=one&state=two",
      `${expected.redirectUris[0]}?code=one`,
      `${expected.redirectUris[0]}?code=one&state=two&next=https://evil.example`,
      `${expected.redirectUris[0]}?code=one&state=two&iss=https://evil.example`,
    ]) {
      expect(() =>
        assertDesktopOAuthRedirect(bad, expected.redirectUris, issuer),
      ).toThrowError("not permitted");
    }
  });
});
