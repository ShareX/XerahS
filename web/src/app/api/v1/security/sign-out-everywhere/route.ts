import { createUserDatabaseClient } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { enforceSameOriginMutation } from "@/lib/request";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/** Ends every browser session and desktop authorization of the current user. */
export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const database = await createUserDatabaseClient(request);
    const revoked = await rpc<number>(database, "sign_out_everywhere");
    return json({ revoked });
  });
}
