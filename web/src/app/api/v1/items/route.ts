import { z } from "zod";

import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc, type GalleryItem } from "@/lib/database";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

const querySchema = z
  .object({
    cursor: z
      .string()
      .regex(/^[A-Za-z0-9_-]{16,1024}$/)
      .optional(),
    limit: z.coerce.number().int().min(1).max(50).default(50),
    kind: z.enum(["screenshot", "screencast"]).optional(),
    from: z.iso.datetime({ offset: true }).optional(),
    to: z.iso.datetime({ offset: true }).optional(),
  })
  .refine(
    ({ from, to }) => !from || !to || from <= to,
    "The date range is invalid.",
  );

export async function GET(request: Request) {
  return handleApi(request, async () => {
    await requireAuthenticatedUser(request, { strong: true });
    const url = new URL(request.url);
    const query = querySchema.parse(Object.fromEntries(url.searchParams));
    const result = await rpc<{
      items: GalleryItem[];
      nextCursor: string | null;
    }>(await createUserDatabaseClient(request), "list_my_gallery_items", {
      p_cursor: query.cursor,
      p_limit: query.limit,
      p_kind: query.kind,
      p_from: query.from,
      p_to: query.to,
    });
    return json(result);
  });
}
