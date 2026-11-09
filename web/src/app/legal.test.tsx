import { renderToStaticMarkup } from "react-dom/server";
import { beforeEach, describe, expect, it, vi } from "vitest";

import RootLayout from "@/app/layout";
import PrivacyPage from "@/app/legal/privacy/page";
import RefundsPage from "@/app/legal/refunds/page";
import TermsPage from "@/app/legal/terms/page";
import { SettingsControls } from "@/components/settings-controls";
import { getOptionalAuthenticatedUser } from "@/lib/auth";
import { checkoutConsentMessage } from "@/lib/legal";

vi.mock("@/lib/auth", () => ({ getOptionalAuthenticatedUser: vi.fn() }));
vi.mock("@/lib/auth-client", () => ({
  authClient: {},
  authErrorMessage: () => "",
}));
vi.mock("next/navigation", () => ({
  redirect: vi.fn(),
  useRouter: () => ({ refresh: vi.fn(), replace: vi.fn() }),
}));
vi.mock("@/lib/env", () => ({
  getPublicEnv: () => ({
    NEXT_PUBLIC_PASSKEYS_ENABLED: false,
    NEXT_PUBLIC_SUPPORT_EMAIL: "help@example.com",
  }),
}));

describe("legal pages", () => {
  beforeEach(() => vi.clearAllMocks());

  it("states the price, trial and cancellation terms and the consumer-law carve-out", () => {
    const html = renderToStaticMarkup(<TermsPage />);
    expect(html).toContain("USD 1.99 per");
    expect(html).toContain("USD 19.99 per year");
    expect(html).toContain("7-day trial");
    expect(html).toContain("Australian Consumer Law");
    expect(html).toContain("not currently registered");
    expect(html).toContain("mailto:help@example.com");
  });

  it("names the processors and the deletion behaviour", () => {
    const html = renderToStaticMarkup(<PrivacyPage />);
    for (const name of ["Stripe", "Neon", "Vercel", "Cloudflare", "Resend"])
      expect(html).toContain(name);
    expect(html).toContain("never see them");
  });

  it("keeps the consumer-law carve-out on the refund policy", () => {
    expect(renderToStaticMarkup(<RefundsPage />)).toContain(
      "Australian Consumer Law",
    );
  });

  it("links all three documents from the site footer", async () => {
    vi.mocked(getOptionalAuthenticatedUser).mockResolvedValue(null);
    const html = renderToStaticMarkup(
      await RootLayout({ children: <div>Content</div> }),
    );
    for (const path of ["/legal/terms", "/legal/privacy", "/legal/refunds"])
      expect(html).toContain(`href="${path}"`);
  });

  it("puts absolute legal links in the Checkout wording", () => {
    const message = checkoutConsentMessage("https://cloud.xerahs.com/");
    expect(message).toContain("(https://cloud.xerahs.com/legal/terms)");
    expect(message).toContain("(https://cloud.xerahs.com/legal/privacy)");
    expect(message).toContain("(https://cloud.xerahs.com/legal/refunds)");
  });
});

describe("billing actions", () => {
  it("disable subscribing until the terms are accepted", () => {
    const html = renderToStaticMarkup(
      <SettingsControls strongAuth trialStatus="not_started" />,
    );
    for (const label of [
      "Start 7-day trial",
      "Subscribe monthly",
      "Subscribe yearly",
    ]) {
      const button = new RegExp(`<button[^>]*disabled=""[^>]*>${label}`);
      expect(html).toMatch(button);
    }
  });

  it("shows the confirming banner after a successful checkout", () => {
    const html = renderToStaticMarkup(
      <SettingsControls
        checkout="success"
        strongAuth
        subscriptionStatus={null}
        trialStatus="active"
      />,
    );
    expect(html).toContain("Confirming your subscription");
  });

  it("shows the active banner when the subscription is already active", () => {
    const html = renderToStaticMarkup(
      <SettingsControls
        checkout="success"
        strongAuth
        subscriptionStatus="active"
        trialStatus="active"
      />,
    );
    expect(html).toContain("Your subscription is active");
  });

  it("reassures the user after a cancelled checkout", () => {
    const html = renderToStaticMarkup(
      <SettingsControls checkout="cancelled" strongAuth trialStatus="active" />,
    );
    expect(html).toContain("You have not been charged");
  });
});
