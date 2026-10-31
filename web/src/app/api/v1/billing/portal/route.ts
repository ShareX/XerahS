import { ApiError } from "@/lib/errors";
import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { getServerEnv } from "@/lib/env";
import { enforceSameOriginMutation } from "@/lib/request";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { getStripeClient } from "@/lib/stripe";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    const env = getServerEnv();
    if (!env.STRIPE_PORTAL_CONFIGURATION_ID)
      throw new ApiError(
        503,
        "integration_unavailable",
        "Billing is not configured.",
      );
    const customerId = await rpc<string>(
      await createUserDatabaseClient(request),
      "get_my_stripe_customer_id",
    );
    if (!customerId)
      throw new ApiError(409, "conflict", "No billing account exists yet.");
    const session = await getStripeClient().billingPortal.sessions.create({
      customer: customerId,
      configuration: env.STRIPE_PORTAL_CONFIGURATION_ID,
      return_url: `${env.APP_ORIGIN}/settings`,
    });
    return json({ url: session.url }, { status: 201 });
  });
}
