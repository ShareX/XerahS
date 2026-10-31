import "server-only";

import { createHmac } from "node:crypto";

import { rpc, serviceDatabaseClient } from "@/lib/database";
import { getServerEnv } from "@/lib/env";

const normalizationVersion = 1;
const hmacKeyVersion = 1;

export function normalizeVerifiedIdentity(email: string): string {
  // Deliberately do not apply provider-specific dot or plus-address rewriting.
  return email.trim().normalize("NFC").toLocaleLowerCase("en-US");
}

export async function registerVerifiedIdentity(
  userId: string,
  verifiedEmail: string,
): Promise<void> {
  const secret = getServerEnv().IDENTITY_HMAC_SECRET_V1;
  if (!secret) throw new Error("IDENTITY_HMAC_SECRET_V1 is not configured.");
  const identityHmac = createHmac("sha256", secret)
    .update(normalizeVerifiedIdentity(verifiedEmail), "utf8")
    .digest("hex");
  await rpc(serviceDatabaseClient(), "register_verified_identity", {
    p_user_id: userId,
    p_identity_hmac: `\\x${identityHmac}`,
    p_normalization_version: normalizationVersion,
    p_hmac_key_version: hmacKeyVersion,
  });
}
