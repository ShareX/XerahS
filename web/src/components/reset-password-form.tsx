"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";

import { authClient, authErrorMessage } from "@/lib/auth-client";

export function ResetPasswordForm({ token }: { token: string }) {
  const [message, setMessage] = useState("");
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const newPassword = String(
      new FormData(event.currentTarget).get("password") ?? "",
    );
    setBusy(true);
    setMessage("");
    const result = await authClient.resetPassword({ newPassword, token });
    setBusy(false);
    if (result.error) return setMessage(authErrorMessage(result.error));
    setDone(true);
    setMessage(
      "Your password was changed and your other sessions were signed out. Sign in with the new password.",
    );
  }

  return (
    <form className="stack" onSubmit={submit}>
      {!done && (
        <>
          <label>
            New password
            <input
              autoComplete="new-password"
              minLength={12}
              name="password"
              required
              type="password"
            />
          </label>
          <button className="primary" disabled={busy} type="submit">
            {busy ? "Working…" : "Change password"}
          </button>
        </>
      )}
      {done && (
        <Link className="button primary" href="/auth">
          Sign in
        </Link>
      )}
      <p
        aria-live="polite"
        className={message && !done ? "status error" : "status"}
      >
        {message}
      </p>
    </form>
  );
}
