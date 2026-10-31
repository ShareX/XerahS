import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { enforceSameOriginMutation } from "@/lib/request";
import { json, pending } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { registerVerifiedIdentity } from "@/lib/identity";
import { attemptImmediateLedgerDispatch } from "@/lib/ledger/dispatcher";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface TrialResult {
  operationId: string;
  replicated: boolean;
  status: "trial_pending" | "active";
}

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const user = await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    await registerVerifiedIdentity(user.id, user.email);
    const database = await createUserDatabaseClient(request);
    let result = await rpc<TrialResult>(database, "start_my_trial");
    if (!result.replicated) {
      await attemptImmediateLedgerDispatch(1);
      result = await rpc<TrialResult>(database, "start_my_trial");
    }
    return result.status === "active" || result.replicated
      ? json({ status: result.status }, { status: 201 })
      : pending(result.operationId);
  });
}
