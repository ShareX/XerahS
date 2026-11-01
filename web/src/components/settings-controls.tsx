"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { authClient, authErrorMessage } from "@/lib/auth-client";
import { waitForActiveSubscription } from "@/lib/billing-poll";

interface Props {
  strongAuth: boolean;
  /** False when the last second-factor check is older than billing accepts. */
  recentStrong?: boolean;
  trialStatus: string;
  subscriptionStatus?: string | null;
  checkout?: string;
}

const STRONG_AUTH_EXPIRED =
  "Your authenticator check has expired. Enter a fresh code under Two-factor authentication above, then try again.";

type Confirmation = "idle" | "waiting" | "active" | "timeout";

export function SettingsControls({
  strongAuth,
  recentStrong = true,
  trialStatus,
  subscriptionStatus = null,
  checkout,
}: Props) {
  const router = useRouter();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [agreed, setAgreed] = useState(false);
  const [confirmation, setConfirmation] = useState<Confirmation>(
    checkout === "success"
      ? subscriptionStatus === "active"
        ? "active"
        : "waiting"
      : "idle",
  );

  useEffect(() => {
    if (confirmation !== "waiting") return;
    let cancelled = false;
    void waitForActiveSubscription(async () => {
      const response = await fetch("/api/v1/me", { cache: "no-store" });
      return response.ok ? await response.json() : null;
    }).then((result) => {
      if (cancelled) return;
      setConfirmation(result);
      if (result === "active") router.refresh();
    });
    return () => {
      cancelled = true;
    };
  }, [confirmation, router]);
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([]);

  async function post(path: string, body?: unknown) {
    setBusy(true);
    setMessage("");
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json", Origin: location.origin },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const result = (await response.json().catch(() => ({}))) as {
      url?: string;
      error?: { code?: string; message?: string };
    };
    setBusy(false);
    if (!response.ok) {
      setMessage(
        result.error?.code === "strong_auth_required"
          ? STRONG_AUTH_EXPIRED
          : (result.error?.message ?? "The request failed."),
      );
      return;
    }
    if (result.url) location.assign(result.url);
    else {
      setMessage(
        response.status === 202 ? "The operation is safely queued." : "Done.",
      );
      router.refresh();
    }
  }

  async function signOut(scope: "local" | "global") {
    setBusy(true);
    setMessage("");
    if (scope === "global") {
      // Ends every browser session and revokes desktop authorizations.
      const response = await fetch("/api/v1/security/sign-out-everywhere", {
        method: "POST",
        headers: { Origin: location.origin },
      });
      if (!response.ok) {
        setBusy(false);
        setMessage("Signing out everywhere failed. Try again.");
        return;
      }
    } else {
      const { error } = await authClient.signOut();
      if (error) {
        setBusy(false);
        setMessage(authErrorMessage(error));
        return;
      }
    }
    location.replace("/");
  }

  async function exportAccount() {
    setBusy(true);
    setMessage("");
    const response = await fetch("/api/v1/account/export", {
      method: "POST",
      headers: { Origin: location.origin },
    });
    setBusy(false);
    if (!response.ok)
      return setMessage("The account export could not be created.");
    const url = URL.createObjectURL(await response.blob());
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = "xerahs-cloud-export.json";
    anchor.click();
    URL.revokeObjectURL(url);
    setMessage("Account export downloaded.");
  }

  async function deleteAccount() {
    if (
      prompt(
        "Type DELETE to request permanent XerahS Cloud account deletion.",
      ) !== "DELETE"
    )
      return;
    setBusy(true);
    setMessage("");
    const response = await fetch("/api/v1/account", {
      method: "DELETE",
      headers: { "Content-Type": "application/json", Origin: location.origin },
      body: JSON.stringify({ confirmation: "DELETE" }),
    });
    setBusy(false);
    if (!response.ok)
      return setMessage("The deletion request could not be queued.");
    setMessage("Account deletion is safely queued. Access is being disabled.");
  }

  async function generateRecoveryCodes() {
    if (
      !confirm(
        "Generate a new recovery-code set? Any unused codes from the previous set will stop working.",
      )
    )
      return;
    setBusy(true);
    setMessage("");
    setRecoveryCodes([]);
    const response = await fetch("/api/v1/security/recovery-codes", {
      method: "POST",
      headers: { Origin: location.origin },
    });
    const result = (await response.json().catch(() => ({}))) as {
      codes?: string[];
      error?: { message?: string };
    };
    setBusy(false);
    if (!response.ok || !result.codes)
      return setMessage(
        result.error?.message ?? "Recovery codes could not be generated.",
      );
    setRecoveryCodes(result.codes);
    setMessage(
      "Save these codes now. They will not be displayed again and each code works only once.",
    );
  }

  return (
    <section className="card stack">
      <h2>Actions</h2>
      {!strongAuth && (
        <p className="error">
          Complete an authenticator challenge in this session before using
          protected gallery and billing actions.
        </p>
      )}
      {strongAuth && !recentStrong && (
        <p className="error" role="status">
          {STRONG_AUTH_EXPIRED}
        </p>
      )}
      {checkout === "success" && confirmation === "waiting" && (
        <p className="notice" role="status">
          Payment received. Confirming your subscription…
        </p>
      )}
      {checkout === "success" && confirmation === "active" && (
        <p className="notice" role="status">
          Your subscription is active. Publishing is enabled.
        </p>
      )}
      {checkout === "success" && confirmation === "timeout" && (
        <p className="notice" role="status">
          Your payment is being confirmed. This can take a minute. Refresh this
          page shortly; if the status does not change, contact support.
        </p>
      )}
      {checkout === "cancelled" && (
        <p className="notice" role="status">
          Checkout was cancelled. You have not been charged.
        </p>
      )}
      <label className="agree">
        <input
          checked={agreed}
          onChange={(event) => setAgreed(event.target.checked)}
          type="checkbox"
        />
        <span>
          I agree to the <Link href="/legal/terms">Terms</Link>, the{" "}
          <Link href="/legal/privacy">Privacy Policy</Link> and the{" "}
          <Link href="/legal/refunds">Refund Policy</Link>.
        </span>
      </label>
      {trialStatus === "not_started" && (
        <button
          className="primary"
          disabled={busy || !agreed}
          onClick={() => void post("/api/v1/trial/start")}
        >
          Start 7-day trial
        </button>
      )}
      <div className="actions">
        <button
          disabled={busy || !agreed}
          onClick={() =>
            void post("/api/v1/billing/checkout", { plan: "monthly" })
          }
        >
          Subscribe monthly · $1.99 USD
        </button>
        <button
          disabled={busy || !agreed}
          onClick={() =>
            void post("/api/v1/billing/checkout", { plan: "annual" })
          }
        >
          Subscribe yearly · $19.99 USD
        </button>
        <button
          disabled={busy}
          onClick={() => void post("/api/v1/billing/portal")}
        >
          Manage billing
        </button>
        <button disabled={busy} onClick={() => void exportAccount()}>
          Export account data
        </button>
        <button disabled={busy} onClick={() => void generateRecoveryCodes()}>
          Generate recovery codes
        </button>
        <button
          className="danger"
          disabled={busy}
          onClick={() => void deleteAccount()}
        >
          Delete XerahS Cloud account
        </button>
        <button disabled={busy} onClick={() => void signOut("local")}>
          Sign out
        </button>
        <button disabled={busy} onClick={() => void signOut("global")}>
          Sign out all devices
        </button>
      </div>
      {recoveryCodes.length > 0 && (
        <div className="stack" role="region" aria-label="New recovery codes">
          <strong>New recovery codes (shown once)</strong>
          <ol>
            {recoveryCodes.map((code) => (
              <li key={code}>
                <code>{code}</code>
              </li>
            ))}
          </ol>
        </div>
      )}
      <p aria-live="polite" className="status">
        {message}
      </p>
    </section>
  );
}
