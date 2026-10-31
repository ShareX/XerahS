import type Stripe from "stripe";

import { rpc, serviceDatabaseClient } from "@/lib/database";
import { ApiError } from "@/lib/errors";
import { getServerEnv } from "@/lib/env";
import { empty } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { getStripeClient, handledStripeEvents } from "@/lib/stripe";
import {
  checkoutMetadata,
  retrieveCanonicalStripeEntitlement,
  stripeCustomerId,
  subscriptionIdFromEvent,
} from "@/lib/stripe-entitlement";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

function objectValue(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null
    ? (value as Record<string, unknown>)
    : {};
}

function boundedErrorCode(error: unknown): string {
  const type =
    typeof error === "object" && error !== null && "type" in error
      ? String(error.type)
      : error instanceof Error
        ? error.name
        : "UNKNOWN_ERROR";
  return type
    .toUpperCase()
    .replaceAll(/[^A-Z0-9_:-]/g, "_")
    .slice(0, 64);
}

async function applyDispute(event: Stripe.Event): Promise<void> {
  const eventDispute = objectValue(event.data.object);
  const disputeId = stripeCustomerId(eventDispute);
  if (!disputeId || !/^dp_[A-Za-z0-9_]{8,255}$/.test(disputeId))
    throw new Error("Stripe dispute identifier is invalid.");
  // Webhook deliveries can arrive out of order (including events created in the
  // same second). Reduce from Stripe's current Dispute instead of the embedded
  // event snapshot so an older delivery cannot re-suspend a won dispute.
  const dispute = await getStripeClient().disputes.retrieve(disputeId);
  const chargeId = stripeCustomerId(dispute.charge);
  const customerId = chargeId
    ? stripeCustomerId(
        (await getStripeClient().charges.retrieve(chargeId)).customer,
      )
    : null;
  if (!customerId) throw new Error("Stripe dispute customer is missing.");
  const suspended = dispute.status !== "won";
  await rpc(serviceDatabaseClient(), "apply_stripe_dispute", {
    p_event_id: event.id,
    p_event_type: event.type,
    p_created_at: new Date(event.created * 1_000).toISOString(),
    p_livemode: event.livemode,
    p_customer_id: customerId,
    p_suspended: suspended,
  });
}

async function processEvent(event: Stripe.Event): Promise<void> {
  const service = serviceDatabaseClient();
  const createdAt = new Date(event.created * 1_000).toISOString();
  if (event.type.startsWith("checkout.session.")) {
    const metadata = checkoutMetadata(event);
    await rpc(service, "record_stripe_checkout_event", {
      p_event_id: event.id,
      p_session_id: metadata.sessionId,
      p_user_id: metadata.userId,
      p_attempt_id: metadata.attemptId,
      p_plan: metadata.plan,
      p_event_type: event.type,
      p_created_at: createdAt,
      p_livemode: event.livemode,
    });
    return;
  }
  if (
    event.type === "charge.dispute.created" ||
    event.type === "charge.dispute.closed"
  ) {
    await applyDispute(event);
    return;
  }
  const subscriptionId = subscriptionIdFromEvent(event);
  if (!subscriptionId) throw new Error("Stripe event subscription is missing.");
  const canonical = await retrieveCanonicalStripeEntitlement(subscriptionId);
  const userId = await rpc<string>(service, "resolve_stripe_webhook_owner", {
    p_customer_id: canonical.customerId,
    p_metadata_user_id: canonical.metadataUserId,
  });
  await rpc(service, "apply_stripe_entitlement", {
    p_event_id: event.id,
    p_event_type: event.type,
    p_stripe_created_at: createdAt,
    p_livemode: event.livemode,
    p_user_id: userId,
    p_result_status: canonical.status,
    p_reason: canonical.catalogAllowed
      ? event.type
      : "stripe_catalog_not_allowlisted",
    p_customer_id: canonical.customerId,
    p_subscription_id: canonical.subscriptionId,
    p_price_id: canonical.priceId,
    p_paid_through: canonical.paidThrough,
    p_grace_started_at: canonical.status === "past_due" ? createdAt : null,
    p_dispute_suspended: false,
  });
}

export async function POST(request: Request) {
  return handleApi(request, async () => {
    const env = getServerEnv();
    const signature = request.headers.get("stripe-signature");
    if (!signature || !env.STRIPE_WEBHOOK_SECRET)
      throw new ApiError(
        400,
        "invalid_request",
        "The webhook signature is missing.",
      );
    const rawBody = await request.text();
    let event: Stripe.Event;
    try {
      event = getStripeClient().webhooks.constructEvent(
        rawBody,
        signature,
        env.STRIPE_WEBHOOK_SECRET,
      );
    } catch {
      throw new ApiError(
        400,
        "invalid_request",
        "The webhook signature is invalid.",
      );
    }
    if (event.livemode !== env.STRIPE_EXPECT_LIVEMODE)
      throw new ApiError(
        400,
        "invalid_request",
        "Webhook mode does not match this environment.",
      );
    if (!handledStripeEvents.has(event.type)) return empty(200);
    try {
      await processEvent(event);
    } catch (error) {
      const errorCode = boundedErrorCode(error);
      console.error("stripe_webhook_processing_failed", {
        eventId: event.id,
        eventType: event.type,
        errorCode,
      });
      await rpc(serviceDatabaseClient(), "record_stripe_webhook_failure", {
        p_event_id: event.id,
        p_event_type: event.type,
        p_created_at: new Date(event.created * 1_000).toISOString(),
        p_livemode: event.livemode,
        p_error_code: errorCode,
      });
      throw error;
    }
    return empty(200);
  });
}
