import { randomUUID } from "node:crypto";

import { rpc, serviceDatabaseClient } from "@/lib/database";
import { getServerEnv } from "@/lib/env";
import { requireCronAuthorization } from "@/lib/internal-auth";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { retrieveCanonicalStripeEntitlement } from "@/lib/stripe-entitlement";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface ReconciliationTarget {
  user_id: string;
  stripe_customer_id: string;
  stripe_subscription_id: string;
}

function errorCode(error: unknown): string {
  return (error instanceof Error ? error.name : "UNKNOWN_ERROR")
    .toUpperCase()
    .replaceAll(/[^A-Z0-9_:-]/g, "_")
    .slice(0, 64);
}

async function reconcile(request: Request) {
  return handleApi(request, async () => {
    requireCronAuthorization(request);
    const service = serviceDatabaseClient();
    const targets = await rpc<ReconciliationTarget[]>(
      service,
      "list_stripe_reconciliation_targets",
      { p_limit: 100 },
    );
    let reconciled = 0;
    let catalogRejected = 0;
    let failed = 0;
    const now = new Date();
    const createdAt = now.toISOString();
    for (const target of targets) {
      const eventId = `reconcile:${randomUUID()}`;
      try {
        const canonical = await retrieveCanonicalStripeEntitlement(
          target.stripe_subscription_id,
        );
        if (canonical.customerId !== target.stripe_customer_id)
          throw new Error("Stripe reconciliation customer mismatch.");
        await rpc(service, "apply_stripe_entitlement", {
          p_event_id: eventId,
          p_event_type: "stripe.reconciliation",
          p_stripe_created_at: createdAt,
          p_livemode: getServerEnv().STRIPE_EXPECT_LIVEMODE,
          p_user_id: target.user_id,
          p_result_status: canonical.status,
          p_reason: canonical.catalogAllowed
            ? "stripe_reconciliation"
            : "stripe_catalog_not_allowlisted",
          p_customer_id: canonical.customerId,
          p_subscription_id: canonical.subscriptionId,
          p_price_id: canonical.priceId,
          p_paid_through: canonical.paidThrough,
          p_grace_started_at:
            canonical.status === "past_due" ? createdAt : null,
          p_dispute_suspended: false,
        });
        reconciled += 1;
        if (!canonical.catalogAllowed) catalogRejected += 1;
      } catch (error) {
        failed += 1;
        const failure = errorCode(error);
        console.error("stripe_reconciliation_failed", {
          subscriptionId: target.stripe_subscription_id,
          errorCode: failure,
        });
        await rpc(service, "record_stripe_webhook_failure", {
          p_event_id: eventId,
          p_event_type: "stripe.reconciliation",
          p_created_at: createdAt,
          p_livemode: getServerEnv().STRIPE_EXPECT_LIVEMODE,
          p_error_code: failure,
        });
      }
    }
    return json({
      scanned: targets.length,
      reconciled,
      catalogRejected,
      failed,
    });
  });
}

export const GET = reconcile;
export const POST = reconcile;
