"use client";

import { oauthProviderClient } from "@better-auth/oauth-provider/client";
import { passkeyClient } from "@better-auth/passkey/client";
import { twoFactorClient } from "better-auth/client/plugins";
import { createAuthClient } from "better-auth/react";

export const authClient = createAuthClient({
  basePath: "/api/auth",
  plugins: [twoFactorClient(), passkeyClient(), oauthProviderClient()],
});

/** Human-readable message from a Better Auth client error. */
export function authErrorMessage(
  error: { message?: string; statusText?: string } | null | undefined,
): string {
  return (
    error?.message || error?.statusText || "The request failed. Try again."
  );
}
