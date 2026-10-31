import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { monthSchema } from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET(request: Request) {
  return handleApi(request, async () => {
    await requireAuthenticatedUser(request, { strong: true });
    const month = monthSchema.parse(
      new URL(request.url).searchParams.get("month"),
    );
    const days = await rpc<Array<{ day: string; count: number }>>(
      await createUserDatabaseClient(request),
      "get_my_gallery_calendar",
      { p_month: month },
    );
    return json({ month, days });
  });
}
