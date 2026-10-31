import { createHmac, randomBytes, randomUUID } from "node:crypto";

import { requireAuthenticatedUser } from "@/lib/auth";
import { rpc, serviceDatabaseClient } from "@/lib/database";
import { ApiError } from "@/lib/errors";
import { getServerEnv } from "@/lib/env";
import { enforceSameOriginMutation } from "@/lib/request";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

function recoveryCode(): string {
  const bytes = randomBytes(20);
  const characters = Array.from(
    bytes,
    (byte) => alphabet[byte % alphabet.length],
  ).join("");
  return characters.match(/.{1,5}/g)!.join("-");
}

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const user = await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    const pepper = getServerEnv().RECOVERY_CODE_PEPPER_V1;
    if (!pepper)
      throw new ApiError(
        503,
        "integration_unavailable",
        "Recovery-code generation is not configured.",
      );

    const codes = Array.from({ length: 10 }, recoveryCode);
    const hashes = codes.map(
      (code) => `\\x${createHmac("sha256", pepper).update(code).digest("hex")}`,
    );
    await rpc<number>(serviceDatabaseClient(), "replace_recovery_code_batch", {
      p_user_id: user.id,
      p_batch_id: randomUUID(),
      p_code_hmacs: hashes,
      p_pepper_version: 1,
    });

    return json(
      { codes },
      { headers: { "Cache-Control": "private, no-store, max-age=0" } },
    );
  });
}
