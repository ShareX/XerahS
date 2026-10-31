import { z } from "zod";

import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { enforceSameOriginMutation, readJson } from "@/lib/request";
import { pending } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { attemptImmediateLedgerDispatch } from "@/lib/ledger/dispatcher";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const deleteSchema = z.object({ confirmation: z.literal("DELETE") });

export async function DELETE(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    deleteSchema.parse(await readJson(request));
    const suppliedKey = request.headers.get("idempotency-key");
    const idempotencyKey =
      suppliedKey && z.uuid().safeParse(suppliedKey).success
        ? suppliedKey
        : crypto.randomUUID();
    const operationId = await rpc<string>(
      await createUserDatabaseClient(request),
      "request_gallery_account_deletion",
      { p_idempotency_key: idempotencyKey },
    );
    await attemptImmediateLedgerDispatch(1);
    return pending(operationId);
  });
}
