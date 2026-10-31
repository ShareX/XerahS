import { NextResponse } from "next/server";
import { z } from "zod";

import { requireAuthenticatedUser } from "@/lib/auth";
import { API_AUDIENCE, DESKTOP_CLIENT_ID, auth } from "@/lib/better-auth";
import { getServerEnv } from "@/lib/env";
import { ApiError } from "@/lib/errors";
import {
  assertDesktopOAuthRedirect,
  desktopOAuthRedirectUris,
  parseDesktopAuthorizationQuery,
} from "@/lib/oauth-validation";
import { enforceSameOriginMutation } from "@/lib/request";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const decisionSchema = z.object({
  oauth_query: z.string().min(1).max(8_192),
  decision: z.enum(["approve", "deny"]),
});

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const contentLength = Number(request.headers.get("content-length") ?? "0");
    if (Number.isFinite(contentLength) && contentLength > 16_384)
      throw new ApiError(413, "invalid_request", "The request is too large.");

    const input = decisionSchema.parse(
      Object.fromEntries((await request.formData()).entries()),
    );
    const env = getServerEnv();
    const redirectUris = desktopOAuthRedirectUris(env.APP_ORIGIN);
    const authorization = parseDesktopAuthorizationQuery(
      new URLSearchParams(input.oauth_query),
      {
        clientId: DESKTOP_CLIENT_ID,
        redirectUris,
        audience: API_AUDIENCE,
      },
    );
    // Approval creates the desktop grant, which requires a second factor.
    await requireAuthenticatedUser(request, {
      strong: true,
      verifiedEmail: true,
    });

    // Hand the decision to Better Auth's own handler as a real request: after
    // consent it re-enters /oauth2/authorize, which needs the Request object.
    const consentRequest = new Request(
      new URL("/api/auth/oauth2/consent", env.APP_ORIGIN),
      {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          Cookie: request.headers.get("cookie") ?? "",
          Origin: new URL(env.APP_ORIGIN).origin,
        },
        body: JSON.stringify({
          accept: input.decision === "approve",
          oauth_query: authorization.oauthQuery,
        }),
      },
    );
    const consentResponse = await auth.handler(consentRequest);
    const result = (await consentResponse.json().catch(() => ({}))) as {
      url?: string;
      redirect_uri?: string;
      error?: string;
      error_description?: string;
    };
    if (!consentResponse.ok) {
      console.warn("oauth_consent_failed", {
        status: consentResponse.status,
        error: result.error,
        description: result.error_description,
      });
      throw new ApiError(
        400,
        "invalid_request",
        "The authorization decision could not be completed.",
      );
    }
    const redirectUrl = result.url ?? result.redirect_uri;
    if (!redirectUrl)
      throw new ApiError(
        400,
        "invalid_request",
        "The authorization decision could not be completed.",
      );

    let target: URL;
    try {
      target = assertDesktopOAuthRedirect(
        redirectUrl,
        redirectUris,
        env.APP_ORIGIN.replace(/\/$/, ""),
      );
    } catch (error) {
      // Shape only: never log codes or state values.
      const shape = URL.canParse(redirectUrl) ? new URL(redirectUrl) : null;
      console.warn("oauth_consent_redirect_rejected", {
        origin: shape?.origin,
        path: shape?.pathname,
        params: shape ? [...shape.searchParams.keys()] : null,
        // A relative target is Better Auth's own error page, not a code carrier.
        relative: shape ? undefined : redirectUrl.slice(0, 200),
      });
      throw error;
    }
    const response = NextResponse.redirect(target, 303);
    response.headers.set("Cache-Control", "private, no-store, max-age=0");
    response.headers.set("Referrer-Policy", "no-referrer");
    return response;
  });
}
