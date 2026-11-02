import "server-only";

import Stripe from "stripe";

import { getServerEnv } from "@/lib/env";

let client: Stripe | undefined;

export function getStripeClient(): Stripe {
  const key = getServerEnv().STRIPE_SECRET_KEY;
  if (!key) throw new Error("STRIPE_SECRET_KEY is not configured.");
  client ??= new Stripe(key, {
    apiVersion: "2026-07-29.dahlia",
    appInfo: {
      name: "XerahS Cloud",
      version: "0.1.0",
      url: "https://xerahs.com",
    },
    maxNetworkRetries: 2,
    timeout: 20_000,
  });
  return client;
}

// Must be stable: Stripe ties an idempotency key to the exact request body, so a
// random value here made every retry of a failed Checkout attempt fail with an
// idempotency_error.
export function integrationIdentifier(): string {
  return "xerahs_cloud";
}

export const handledStripeEvents = new Set<Stripe.Event.Type>([
  "checkout.session.completed",
  "checkout.session.expired",
  "checkout.session.async_payment_succeeded",
  "checkout.session.async_payment_failed",
  "customer.subscription.created",
  "customer.subscription.updated",
  "customer.subscription.deleted",
  "invoice.paid",
  "invoice.payment_failed",
  "invoice.payment_action_required",
  "invoice.finalization_failed",
  "charge.dispute.created",
  "charge.dispute.closed",
]);
