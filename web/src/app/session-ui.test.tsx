import { renderToStaticMarkup } from "react-dom/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

import AuthPage from "@/app/auth/page";
import RootLayout from "@/app/layout";
import HomePage from "@/app/page";
import { getOptionalAuthenticatedUser } from "@/lib/auth";
import { redirect } from "next/navigation";

vi.mock("@/lib/auth", () => ({
  getOptionalAuthenticatedUser: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  redirect: vi.fn(),
}));

vi.mock("@/lib/env", () => ({
  getPublicEnv: () => ({ NEXT_PUBLIC_PASSKEYS_ENABLED: false }),
}));

const user = {
  id: "account-id",
  email: "owner@example.com",
  emailVerified: true,
  aal: "aal2" as const,
  sessionId: "session-id",
  authenticatedAt: new Date("2026-08-27T00:00:00Z"),
  strongMethod: "totp",
};

describe("session-aware account UI", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows account state instead of sign-in actions for a valid session", async () => {
    vi.mocked(getOptionalAuthenticatedUser).mockResolvedValue(user);

    const layout = renderToStaticMarkup(
      await RootLayout({ children: <div>Content</div> }),
    );
    const home = renderToStaticMarkup(await HomePage());

    expect(layout).toContain("Signed in");
    expect(layout).toContain("Account");
    expect(layout).not.toContain(">Sign in<");
    expect(home).toContain("Signed in as owner@example.com");
    expect(home).toContain("Open your account");
    expect(home).not.toContain(">Sign in<");
  });

  it("shows sign-in actions when there is no valid session", async () => {
    vi.mocked(getOptionalAuthenticatedUser).mockResolvedValue(null);

    const layout = renderToStaticMarkup(
      await RootLayout({ children: <div>Content</div> }),
    );
    const home = renderToStaticMarkup(await HomePage());

    expect(layout).toContain(">Sign in<");
    expect(home).toContain(">Sign in<");
    expect(layout).not.toContain("Signed in");
  });

  it("redirects an authenticated visitor away from the sign-in form", async () => {
    vi.mocked(getOptionalAuthenticatedUser).mockResolvedValue(user);

    await AuthPage({ searchParams: Promise.resolve({ next: "/settings" }) });

    expect(redirect).toHaveBeenCalledWith("/settings");
  });

  it("rejects a protocol-relative post-auth redirect", async () => {
    vi.mocked(getOptionalAuthenticatedUser).mockResolvedValue(user);

    await AuthPage({ searchParams: Promise.resolve({ next: "//evil.test" }) });

    expect(redirect).toHaveBeenCalledWith("/settings");
  });

  it("prevents an authenticated visitor from redirecting back to auth", async () => {
    vi.mocked(getOptionalAuthenticatedUser).mockResolvedValue(user);

    await AuthPage({ searchParams: Promise.resolve({ next: "/auth" }) });

    expect(redirect).toHaveBeenCalledWith("/settings");
  });
});
