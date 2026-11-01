import type { Metadata } from "next";

import { LegalPage } from "@/components/legal-page";
import { GOVERNING_LAW, LEGAL_ENTITY } from "@/lib/legal";

export const metadata: Metadata = { title: "Terms of Service" };

export default function TermsPage() {
  return (
    <LegalPage title="Terms of Service">
      <h2>The service</h2>
      <p>
        XerahS Cloud is an optional online service from {LEGAL_ENTITY} for
        owners of the XerahS app. When you choose Publish in XerahS, the link to
        a screenshot or screencast you already uploaded is registered in your
        private gallery, with a title taken from the file name, a thumbnail link
        and the capture date. Only you can open your gallery. XerahS Cloud does
        not store, host or proxy the image or video files themselves; they stay
        at the destination you uploaded them to.
      </p>
      <h2>Your account</h2>
      <p>
        You need a verified email address and a second sign-in factor (an
        authenticator app or a passkey) to publish and to manage billing. Keep
        your credentials and recovery codes safe. You are responsible for
        activity on your account.
      </p>
      <h2>Free trial</h2>
      <p>
        Each account can start one 7-day trial without a card. Publishing stops
        when the trial ends unless you subscribe.
      </p>
      <h2>Subscription and payment</h2>
      <p>
        After the trial, publishing needs a paid subscription: USD 1.99 per
        month or USD 19.99 per year. Payments are processed by Stripe; we never
        see or store your card number. Subscriptions renew automatically at the
        end of each period until you cancel. We are not currently registered for
        GST, so none is added. If that changes we will tell you before your next
        renewal.
      </p>
      <h2>Cancelling</h2>
      <p>
        You can cancel at any time from Manage billing in your account settings.
        Your subscription then stays active until the end of the period you have
        paid for and does not renew.
      </p>
      <h2>When access ends</h2>
      <p>
        If your trial or subscription ends, you can still view your gallery,
        copy or download your links and unpublish items. You cannot publish new
        items until you subscribe again. Deleting your account removes your
        gallery data as described in the Privacy Policy.
      </p>
      <h2>What you may publish</h2>
      <p>
        Only publish links to content you have the right to share, and do not
        publish anything unlawful. We may disable an account that is used to
        abuse the service or that is the subject of a payment dispute.
      </p>
      <h2>Availability and changes</h2>
      <p>
        We aim to keep XerahS Cloud available but do not promise uninterrupted
        service. We may change the service or these terms; for material changes
        we will give you notice before they apply to you, and you may cancel if
        you do not agree.
      </p>
      <h2>Your legal rights</h2>
      <p>
        Nothing in these terms excludes, restricts or modifies any right or
        remedy you have under the Australian Consumer Law or other law that
        cannot be excluded. These terms are governed by the laws of{" "}
        {GOVERNING_LAW}.
      </p>
    </LegalPage>
  );
}
