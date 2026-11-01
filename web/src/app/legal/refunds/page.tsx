import type { Metadata } from "next";

import { LegalPage } from "@/components/legal-page";

export const metadata: Metadata = { title: "Refund Policy" };

export default function RefundsPage() {
  return (
    <LegalPage title="Refund Policy">
      <h2>Cancelling</h2>
      <p>
        You can cancel at any time from Manage billing in your account settings.
        You keep access until the end of the period you have paid for, and you
        are not charged again.
      </p>
      <h2>Refunds</h2>
      <p>
        We do not refund part-used subscription periods. We will refund a charge
        made in error, a duplicate charge, or a renewal you tell us about
        promptly if you had cancelled in time. Refunds go back to the original
        payment method through Stripe and can take several business days to
        appear.
      </p>
      <h2>Your legal rights</h2>
      <p>
        This policy does not limit your rights under the Australian Consumer
        Law, including the right to a refund or other remedy where the service
        has a major failure.
      </p>
    </LegalPage>
  );
}
