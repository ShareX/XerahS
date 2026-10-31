import { z } from "zod";

import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc } from "@/lib/database";
import { enforceSameOriginMutation, readJson } from "@/lib/request";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const profileSchema = z.object({
  slug: z.string().regex(/^[a-z0-9](?:[a-z0-9-]{1,28}[a-z0-9])?$/),
  timeZone: z.string().min(1).max(63),
});

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    await requireAuthenticatedUser(request, {
      strong: true,
      verifiedEmail: true,
    });
    const input = profileSchema.parse(await readJson(request));
    const profile = await rpc(
      await createUserDatabaseClient(request),
      "create_gallery_profile",
      {
        p_slug: input.slug,
        p_time_zone: input.timeZone,
      },
    );
    return json({ profile }, { status: 201 });
  });
}
