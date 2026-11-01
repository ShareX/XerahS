import type { Metadata } from "next";

import { LegalPage } from "@/components/legal-page";
import { LEGAL_ENTITY } from "@/lib/legal";

export const metadata: Metadata = { title: "Privacy Policy" };

export default function PrivacyPage() {
  return (
    <LegalPage title="Privacy Policy">
      <p>
        This policy explains what {LEGAL_ENTITY} collects through XerahS Cloud,
        why, and who handles it. We handle personal information in line with the
        Australian Privacy Principles.
      </p>
      <h2>What we collect</h2>
      <ul>
        <li>
          <strong>Account:</strong> your email address, a username, a hashed
          password, and your second-factor and passkey registrations.
        </li>
        <li>
          <strong>Sessions:</strong> sign-in session records and the desktop
          authorizations you grant, so you can see and revoke them.
        </li>
        <li>
          <strong>Gallery:</strong> for each item you publish, its link, a title
          taken from the file name, a thumbnail link, the capture date, its kind
          (screenshot or screencast) and the host it was uploaded to. We do not
          receive or store the files.
        </li>
        <li>
          <strong>Billing:</strong> your Stripe customer and subscription
          identifiers and status. Stripe collects and holds your card details;
          we never see them.
        </li>
        <li>
          <strong>Trial and deletion records:</strong> a minimal record that a
          trial was used, and a deletion record that stops a deleted account
          from being re-created with a new trial.
        </li>
      </ul>
      <h2>How we use it</h2>
      <p>
        To run your account and gallery, keep it secure, take payment, send
        verification and password-reset email, and meet legal obligations. We do
        not sell your information or use it for advertising.
      </p>
      <h2>Who processes it</h2>
      <ul>
        <li>Neon (database, Sydney) and Vercel (hosting, Sydney).</li>
        <li>Cloudflare (DNS, scheduled jobs and a private records bucket).</li>
        <li>Resend (transactional email).</li>
        <li>Stripe (payments).</li>
      </ul>
      <p>
        Some of these providers may process data outside Australia. We use them
        only to provide the service.
      </p>
      <h2>Cookies</h2>
      <p>
        We use only the cookies needed to keep you signed in and protect your
        session. There is no advertising or analytics tracking.
      </p>
      <h2>Your choices</h2>
      <p>
        From your account settings you can export your data, generate new
        recovery codes, sign out everywhere and delete your account. Deletion
        removes your gallery and profile and cancels any subscription. The
        minimal deletion record described above is kept for up to 180 days. You
        can also ask to access or correct your information using the contact
        details below.
      </p>
    </LegalPage>
  );
}
