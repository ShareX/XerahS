import { ApiError } from "@/lib/errors";
import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { getServerEnv } from "@/lib/env";
import { enforceSameOriginMutation, readJson } from "@/lib/request";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { getStripeClient, integrationIdentifier } from "@/lib/stripe";
import { planSchema } from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface CheckoutContext {
  attemptId: string;
  customerId: string | null;
  customerIdempotencyKey: string;
  checkoutIdempotencyKey: string;
}

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const user = await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    const { plan } = planSchema.parse(await readJson(request));
    const env = getServerEnv();
    const price =
      plan === "monthly" ? env.STRIPE_PRICE_MONTHLY : env.STRIPE_PRICE_ANNUAL;
    if (!price)
      throw new ApiError(
        503,
        "integration_unavailable",
        "Billing is not configured.",
      );

    const database = await createUserDatabaseClient(request);
    const context = await rpc<CheckoutContext>(
      database,
      "prepare_my_stripe_checkout",
      { p_plan: plan },
    );
    const stripe = getStripeClient();
    let customerId = context.customerId;
    if (!customerId) {
      const customer = await stripe.customers.create(
        { email: user.email, metadata: { xerahs_user_id: user.id } },
        { idempotencyKey: context.customerIdempotencyKey },
      );
      customerId = await rpc<string>(database, "attach_my_stripe_customer", {
        p_customer_id: customer.id,
      });
    }

    const session = await stripe.checkout.sessions.create(
      {
        mode: "subscription",
        customer: customerId,
        client_reference_id: user.id,
        line_items: [{ price, quantity: 1 }],
        success_url: `${env.APP_ORIGIN}/settings?checkout=success`,
        cancel_url: `${env.APP_ORIGIN}/settings?checkout=cancelled`,
        integration_identifier: integrationIdentifier(),
        metadata: {
          xerahs_user_id: user.id,
          xerahs_plan: plan,
          xerahs_checkout_attempt_id: context.attemptId,
        },
        subscription_data: { metadata: { xerahs_user_id: user.id } },
        ...(env.STRIPE_TAX_ENABLED
          ? {
              automatic_tax: { enabled: true },
              customer_update: { address: "auto" },
            }
          : {}),
      },
      { idempotencyKey: context.checkoutIdempotencyKey },
    );
    if (!session.url)
      throw new ApiError(
        502,
        "integration_unavailable",
        "Stripe did not return a Checkout URL.",
      );
    await rpc(database, "finalize_my_stripe_checkout", {
      p_attempt_id: context.attemptId,
      p_session_id: session.id,
    });
    return json({ url: session.url }, { status: 201 });
  });
}
