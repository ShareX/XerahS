import { describe, expect, it } from "vitest";

import { oauthContinueUrl } from "./oauth-continue";

const origin = "https://cloud.xerahs.com";

describe("oauthContinueUrl", () => {
  it("follows Better Auth's same-origin continuation", () => {
    expect(
      oauthContinueUrl(
        { redirect: true, url: "https://cloud.xerahs.com/oauth/consent?x=1" },
        origin,
      ),
    ).toBe("https://cloud.xerahs.com/oauth/consent?x=1");
    expect(
      oauthContinueUrl({ redirect: true, url: "/oauth/consent" }, origin),
    ).toBe("https://cloud.xerahs.com/oauth/consent");
  });

  it("ignores plain sign-in responses", () => {
    expect(oauthContinueUrl(undefined, origin)).toBeNull();
    expect(oauthContinueUrl({ token: "t", user: {} }, origin)).toBeNull();
    expect(
      oauthContinueUrl({ redirect: false, url: "/oauth/consent" }, origin),
    ).toBeNull();
  });

  it("refuses other origins", () => {
    expect(
      oauthContinueUrl(
        { redirect: true, url: "https://evil.example/phish" },
        origin,
      ),
    ).toBeNull();
    expect(
      oauthContinueUrl({ redirect: true, url: "//evil.example/x" }, origin),
    ).toBeNull();
    expect(
      oauthContinueUrl(
        { redirect: true, url: "xerahs://oauth/callback" },
        origin,
      ),
    ).toBeNull();
  });
});
