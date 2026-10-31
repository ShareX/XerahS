import { randomUUID } from "node:crypto";

import { rpc, serviceDatabaseClient } from "@/lib/database";
import { requireCronAuthorization } from "@/lib/internal-auth";
import { json } from "@/lib/responses";
import { handleApi } from "@/lib/route-handler";
import { getStripeClient } from "@/lib/stripe";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

interface ClaimedDeletion {
  request_id: string;
  user_id: string;
  stripe_subscription_id: string | null;
  tombstoned_at: string | null;
  billing_cancelled_at: string | null;
}

function errorCode(error: unknown): string {
  return (error instanceof Error ? error.name : "UNKNOWN_ERROR")
    .toUpperCase()
    .replaceAll(/[^A-Z0-9_:-]/g, "_")
    .slice(0, 64);
}

function alreadyMissing(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    error.code === "resource_missing"
  );
}

async function processDeletions(request: Request) {
  return handleApi(request, async () => {
    requireCronAuthorization(request);
    const service = serviceDatabaseClient();
    const workerId = randomUUID();
    const claimed = await rpc<ClaimedDeletion[]>(
      service,
      "claim_account_deletions",
      { p_worker_id: workerId, p_limit: 25, p_lease_seconds: 300 },
    );
    let billingCancelled = 0;
    let completed = 0;
    let deferred = 0;
    let failed = 0;
    for (const item of claimed) {
      try {
        if (!item.billing_cancelled_at) {
          if (item.stripe_subscription_id) {
            try {
              await getStripeClient().subscriptions.cancel(
                item.stripe_subscription_id,
                {},
                { idempotencyKey: `xerahs-delete-${item.request_id}` },
              );
            } catch (error) {
              if (!alreadyMissing(error)) throw error;
            }
          }
          await rpc(service, "mark_account_deletion_billing_cancelled", {
            p_request_id: item.request_id,
            p_worker_id: workerId,
          });
          billingCancelled += 1;
          continue;
        }
        if (!item.tombstoned_at) {
          await rpc(service, "defer_account_deletion", {
            p_request_id: item.request_id,
            p_worker_id: workerId,
            p_retry_after_seconds: 60,
          });
          deferred += 1;
          continue;
        }
        await rpc(service, "complete_account_deletion", {
          p_request_id: item.request_id,
          p_worker_id: workerId,
        });
        completed += 1;
      } catch (error) {
        failed += 1;
        const failure = errorCode(error);
        console.error("account_deletion_processing_failed", {
          requestId: item.request_id,
          errorCode: failure,
        });
        await rpc(service, "fail_account_deletion", {
          p_request_id: item.request_id,
          p_worker_id: workerId,
          p_error_code: failure,
        });
      }
    }
    return json({
      claimed: claimed.length,
      billingCancelled,
      completed,
      deferred,
      failed,
    });
  });
}

export const GET = processDeletions;
export const POST = processDeletions;
