import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { rpc, type GalleryItem } from "@/lib/database";
import { enforceSameOriginMutation, readJson } from "@/lib/request";
import { empty, json, pending } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { attemptImmediateLedgerDispatch } from "@/lib/ledger/dispatcher";
import {
  clientItemIdSchema,
  publishSchema,
  titleFromFileName,
} from "@/lib/validation";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface RouteContext {
  params: Promise<{ clientItemId: string }>;
}
interface UnpublishResult {
  operationId: string | null;
  replicated: boolean;
}

export async function PUT(request: Request, context: RouteContext) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    await requireAuthenticatedUser(request, {
      strong: true,
      verifiedEmail: true,
    });
    const clientItemId = clientItemIdSchema.parse(
      (await context.params).clientItemId,
    );
    const body = publishSchema.parse(await readJson(request));
    const item = await rpc<GalleryItem>(
      await createUserDatabaseClient(request),
      "publish_gallery_item",
      {
        p_client_item_id: clientItemId,
        p_url: body.url,
        p_thumbnail_url: body.thumbnailUrl,
        p_kind: body.kind,
        p_file_name: body.fileName,
        p_title: titleFromFileName(body.fileName),
        p_captured_at: body.capturedAt,
        p_host: body.host,
        p_content_type: body.contentType,
        p_idempotency_key:
          request.headers.get("idempotency-key") ?? clientItemId,
      },
    );
    return json({ item }, { status: 200 });
  });
}

export async function DELETE(request: Request, context: RouteContext) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    await requireAuthenticatedUser(request, { strong: true });
    const clientItemId = clientItemIdSchema.parse(
      (await context.params).clientItemId,
    );
    const database = await createUserDatabaseClient(request);
    let result = await rpc<UnpublishResult>(
      database,
      "request_gallery_item_unpublish",
      {
        p_client_item_id: clientItemId,
        p_idempotency_key:
          request.headers.get("idempotency-key") ?? `unpublish:${clientItemId}`,
      },
    );
    if (!result.replicated && result.operationId) {
      await attemptImmediateLedgerDispatch(1);
      result = await rpc<UnpublishResult>(
        database,
        "request_gallery_item_unpublish",
        {
          p_client_item_id: clientItemId,
          p_idempotency_key:
            request.headers.get("idempotency-key") ??
            `unpublish:${clientItemId}`,
        },
      );
    }
    if (result.replicated || !result.operationId) return empty();
    return pending(result.operationId);
  });
}
