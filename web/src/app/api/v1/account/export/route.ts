import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { getAccountSummary, query } from "@/lib/database";
import { ApiError } from "@/lib/errors";
import { enforceSameOriginMutation } from "@/lib/request";
import { handleApi } from "@/lib/route-handler";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function POST(request: Request) {
  return handleApi(request, async () => {
    enforceSameOriginMutation(request);
    const user = await requireAuthenticatedUser(request, {
      strong: true,
      recent: true,
      verifiedEmail: true,
    });
    const client = await createUserDatabaseClient(request);
    // Both reads run as the user under RLS, like the PostgREST selects they replace.
    const [summary, profiles, galleryItems] = await Promise.all([
      getAccountSummary(client),
      query<Record<string, unknown>>(
        client,
        "select slug, time_zone, created_at, updated_at from public.profiles",
      ),
      query<Record<string, unknown>>(
        client,
        `select id, client_item_id, url, thumbnail_url, kind, file_name, title, captured_at, published_at, host, content_type
           from public.gallery_items order by captured_at desc`,
      ),
    ]);
    if (profiles.length !== 1)
      throw new ApiError(404, "not_found", "The profile was not found.");
    const profile = { data: profiles[0] };
    const gallery = { data: galleryItems };

    const body = JSON.stringify(
      {
        schemaVersion: 1,
        exportedAt: new Date().toISOString(),
        account: { id: user.id, email: user.email },
        profile: profile.data,
        subscription: {
          status: summary.subscriptionStatus,
          paidThrough: summary.paidThrough,
          trialStatus: summary.trialStatus,
          trialEndsAt: summary.trialEndsAt,
        },
        galleryItems: gallery.data,
        security: { assuranceLevel: user.aal },
      },
      null,
      2,
    );
    return new Response(body, {
      status: 200,
      headers: {
        "Cache-Control": "private, no-store",
        "Content-Disposition":
          'attachment; filename="xerahs-cloud-export.json"',
        "Content-Type": "application/json; charset=utf-8",
      },
    });
  });
}
