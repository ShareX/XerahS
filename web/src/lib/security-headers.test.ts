import { describe, expect, it } from "vitest";

import {
  applyNoStore,
  applySecurityHeaders,
  contentSecurityPolicy,
  isPersonalizedPath,
} from "@/lib/security-headers";

describe("security headers", () => {
  it("builds a nonce CSP that only connects to this origin", () => {
    const policy = contentSecurityPolicy("nonce-value");
    expect(policy).toContain(
      "script-src 'self' 'nonce-nonce-value' 'strict-dynamic'",
    );
    expect(policy).toContain("connect-src 'self';");
    expect(policy).not.toContain("unsafe-eval");
  });

  it("applies production and no-store headers", () => {
    const headers = new Headers();
    applySecurityHeaders(headers, "abc", true);
    applyNoStore(headers);
    expect(headers.get("x-frame-options")).toBe("DENY");
    expect(headers.get("strict-transport-security")).toContain(
      "includeSubDomains",
    );
    expect(headers.get("content-security-policy")).toContain("nonce-abc");
    expect(headers.get("cache-control")).toContain("no-store");
  });

  it("uses report-only CSP outside production", () => {
    const headers = new Headers();
    applySecurityHeaders(headers, "abc", false);
    expect(headers.get("content-security-policy")).toBeNull();
    expect(headers.get("content-security-policy-report-only")).toContain(
      "nonce-abc",
    );
  });

  it("prevents caching every route that can render account state", () => {
    expect(isPersonalizedPath("/")).toBe(true);
    expect(isPersonalizedPath("/auth")).toBe(true);
    expect(isPersonalizedPath("/settings")).toBe(true);
    expect(isPersonalizedPath("/owner-profile")).toBe(true);
    expect(isPersonalizedPath("/nested/static-route")).toBe(false);
  });
});
