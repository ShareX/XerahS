export interface SubscriptionSnapshot {
  subscriptionStatus?: string | null;
}

export interface WaitOptions {
  intervalMs?: number;
  timeoutMs?: number;
  sleep?: (ms: number) => Promise<void>;
}

/**
 * Stripe confirms a payment to the webhook a moment after the customer returns
 * from Checkout. Poll the account summary until it reports an active
 * subscription, or give up after the timeout.
 */
export async function waitForActiveSubscription(
  fetchSummary: () => Promise<SubscriptionSnapshot | null>,
  {
    intervalMs = 2_000,
    timeoutMs = 30_000,
    sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
  }: WaitOptions = {},
): Promise<"active" | "timeout"> {
  let waited = 0;
  for (;;) {
    let summary: SubscriptionSnapshot | null = null;
    try {
      summary = await fetchSummary();
    } catch {
      // A transient failure should not abort the wait.
    }
    if (summary?.subscriptionStatus === "active") return "active";
    if (waited >= timeoutMs) return "timeout";
    await sleep(intervalMs);
    waited += intervalMs;
  }
}
