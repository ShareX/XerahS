import { describe, expect, it, vi } from "vitest";

import { waitForActiveSubscription } from "@/lib/billing-poll";

describe("waitForActiveSubscription", () => {
  const sleep = vi.fn(async () => {});

  it("returns as soon as the subscription is active", async () => {
    const fetchSummary = vi
      .fn()
      .mockResolvedValueOnce({ subscriptionStatus: null })
      .mockResolvedValueOnce({ subscriptionStatus: "active" });
    await expect(
      waitForActiveSubscription(fetchSummary, { sleep }),
    ).resolves.toBe("active");
    expect(fetchSummary).toHaveBeenCalledTimes(2);
  });

  it("times out when it never becomes active", async () => {
    const fetchSummary = vi
      .fn()
      .mockResolvedValue({ subscriptionStatus: null });
    await expect(
      waitForActiveSubscription(fetchSummary, {
        sleep,
        intervalMs: 1_000,
        timeoutMs: 3_000,
      }),
    ).resolves.toBe("timeout");
    expect(fetchSummary).toHaveBeenCalledTimes(4);
  });

  it("survives a failed poll", async () => {
    const fetchSummary = vi
      .fn()
      .mockRejectedValueOnce(new Error("network"))
      .mockResolvedValueOnce({ subscriptionStatus: "active" });
    await expect(
      waitForActiveSubscription(fetchSummary, { sleep }),
    ).resolves.toBe("active");
  });

  it("does not treat a non-active status as success", async () => {
    const fetchSummary = vi
      .fn()
      .mockResolvedValue({ subscriptionStatus: "past_due" });
    await expect(
      waitForActiveSubscription(fetchSummary, {
        sleep,
        intervalMs: 1_000,
        timeoutMs: 1_000,
      }),
    ).resolves.toBe("timeout");
  });
});
