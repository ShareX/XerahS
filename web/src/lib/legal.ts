/** Shared facts for the legal pages and the Checkout wording. */
export const LEGAL_ENTITY = "ShareX Team";
export const LEGAL_COUNTRY = "Australia";
export const GOVERNING_LAW = "Western Australia, Australia";
/** Bump when the Terms, Privacy Policy or Refund Policy change materially. */
export const LEGAL_VERSION = "2026-11-01";

export const LEGAL_LINKS = [
  { href: "/legal/terms", label: "Terms" },
  { href: "/legal/privacy", label: "Privacy" },
  { href: "/legal/refunds", label: "Refunds" },
] as const;

/** Shown by Stripe Checkout above the Subscribe button (Markdown links). */
export function checkoutConsentMessage(origin: string): string {
  const base = origin.replace(/\/$/, "");
  return `By subscribing you agree to the [Terms](${base}/legal/terms), the [Privacy Policy](${base}/legal/privacy) and the [Refund Policy](${base}/legal/refunds). Prices are in US dollars. No GST is added.`;
}
