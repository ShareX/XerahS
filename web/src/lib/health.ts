import "server-only";

import { assertProductionConfiguration, getServerEnv } from "@/lib/env";

type DependencyState = "ready" | "unavailable";

export async function healthResponse(): Promise<Response> {
  try {
    assertProductionConfiguration();
    const server = getServerEnv();
    const configured = {
      database: Boolean(server.DATABASE_URL),
      auth: Boolean(server.BETTER_AUTH_SECRET),
      email: Boolean(server.RESEND_API_KEY && server.EMAIL_FROM),
      oauth: Boolean(server.XERAHS_DESKTOP_OAUTH_CLIENT_ID),
      stripe: Boolean(
        server.STRIPE_SECRET_KEY &&
        server.STRIPE_WEBHOOK_SECRET &&
        server.STRIPE_PRICE_MONTHLY &&
        server.STRIPE_PRICE_ANNUAL &&
        server.STRIPE_PORTAL_CONFIGURATION_ID,
      ),
      ledger:
        server.LEDGER_USE_LOCAL_FAKE ||
        Boolean(
          server.R2_LEDGER_ACCOUNT_ID &&
          server.R2_LEDGER_BUCKET &&
          server.R2_LEDGER_ACCESS_KEY_ID &&
          server.R2_LEDGER_SECRET_ACCESS_KEY &&
          server.LEDGER_HMAC_SECRET_V1,
        ),
    };
    const dependencies = Object.fromEntries(
      Object.entries(configured).map(([name, ready]) => [
        name,
        (ready ? "ready" : "unavailable") satisfies DependencyState,
      ]),
    );
    const ready = Object.values(configured).every(Boolean);
    return Response.json(
      {
        status: ready ? "ready" : "degraded",
        environment: server.APP_ENV,
        dependencies,
      },
      {
        status: ready ? 200 : 503,
        headers: { "Cache-Control": "no-store" },
      },
    );
  } catch {
    return Response.json(
      { status: "unavailable" },
      { status: 503, headers: { "Cache-Control": "no-store" } },
    );
  }
}
