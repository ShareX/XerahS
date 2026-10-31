"use client";

import { useState, type FormEvent } from "react";

export function ConsentDecisionForm({ oauthQuery }: { oauthQuery: string }) {
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const submitter = (event.nativeEvent as SubmitEvent).submitter;
    const decision =
      submitter instanceof HTMLButtonElement ? submitter.value : "deny";
    setBusy(true);
    setMessage("");
    try {
      const body = new FormData();
      body.set("oauth_query", oauthQuery);
      body.set("decision", decision);
      const response = await fetch("/api/oauth/decision", {
        method: "POST",
        body,
        credentials: "same-origin",
      });
      if (response.redirected) {
        window.location.assign(response.url);
        return;
      }
      if (!response.ok) {
        const payload = (await response.json().catch(() => null)) as {
          error?: { message?: string };
        } | null;
        setMessage(
          payload?.error?.message ??
            "The authorization decision could not be completed.",
        );
        setBusy(false);
        return;
      }
      window.location.assign(response.url || "/settings");
    } catch {
      setMessage("The authorization decision could not be completed.");
      setBusy(false);
    }
  }

  return (
    <form className="actions" onSubmit={submit}>
      <button
        className="primary"
        disabled={busy}
        name="decision"
        type="submit"
        value="approve"
      >
        {busy ? "Working…" : "Authorize desktop"}
      </button>
      <button disabled={busy} name="decision" type="submit" value="deny">
        Deny
      </button>
      <p aria-live="polite" className={message ? "status error" : "status"}>
        {message}
      </p>
    </form>
  );
}
