"use client";

import { useState, type FormEvent } from "react";

import { authClient, authErrorMessage } from "@/lib/auth-client";
import { oauthContinueUrl } from "@/lib/oauth-continue";

type Mode = "signin" | "signup" | "second-factor" | "reset";

export function AuthForm({
  next = "/settings",
  passkeysEnabled = false,
}: {
  next?: string;
  passkeysEnabled?: boolean;
}) {
  const [mode, setMode] = useState<Mode>("signin");
  const [useBackupCode, setUseBackupCode] = useState(false);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  function finish(data?: unknown) {
    // Signing in from an OAuth authorize redirect (desktop or iOS app) carries
    // the signed request in the page query; Better Auth then answers with the
    // next step of that authorization (the consent page) instead of a session
    // only. Follow it, same-origin only, or the app never gets its code.
    const continueUrl = oauthContinueUrl(data, location.origin);
    // Start a fresh document request after the auth cookie is written so
    // prefetched anonymous React Server Component data is not reused.
    location.replace(continueUrl ?? next);
  }

  function switchMode(target: Mode) {
    setMode(target);
    setMessage("");
    setUseBackupCode(false);
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage("");
    const data = new FormData(event.currentTarget);
    const email = String(data.get("email") ?? "");
    const password = String(data.get("password") ?? "");
    const code = String(data.get("code") ?? "").trim();

    try {
      if (mode === "signin") {
        const result = await authClient.signIn.email({ email, password });
        if (result.error) return setMessage(authErrorMessage(result.error));
        // Accounts with two-factor authentication get no session until the
        // second factor passes.
        if (
          result.data &&
          "twoFactorRedirect" in result.data &&
          result.data.twoFactorRedirect
        ) {
          return switchMode("second-factor");
        }
        return finish(result.data);
      }

      if (mode === "second-factor") {
        const result = useBackupCode
          ? await authClient.twoFactor.verifyBackupCode({ code })
          : await authClient.twoFactor.verifyTotp({ code });
        if (result.error) return setMessage(authErrorMessage(result.error));
        return finish(result.data);
      }

      if (mode === "signup") {
        const callbackURL = new URL("/auth", location.origin);
        callbackURL.searchParams.set("next", next);
        callbackURL.searchParams.set("verified", "1");
        const result = await authClient.signUp.email({
          email,
          password,
          name: email.split("@")[0] || email,
          callbackURL: callbackURL.pathname + callbackURL.search,
        });
        if (result.error) return setMessage(authErrorMessage(result.error));
        return setMessage(
          "Check your email to verify your account, then sign in.",
        );
      }

      const result = await authClient.requestPasswordReset({
        email,
        redirectTo: "/auth/reset",
      });
      if (result.error) return setMessage(authErrorMessage(result.error));
      setMessage(
        "If an account uses that address, a reset link is on its way.",
      );
    } finally {
      setBusy(false);
    }
  }

  async function signInWithPasskey() {
    setBusy(true);
    setMessage("");
    const result = await authClient.signIn.passkey();
    setBusy(false);
    if (result?.error) return setMessage(authErrorMessage(result.error));
    finish(result?.data);
  }

  if (mode === "second-factor") {
    return (
      <form className="stack" onSubmit={submit}>
        <p>
          {useBackupCode
            ? "Enter one of your unused backup codes."
            : "Enter the six-digit code from your authenticator app."}
        </p>
        <label>
          {useBackupCode ? "Backup code" : "Authenticator code"}
          <input
            autoComplete="one-time-code"
            autoFocus
            inputMode={useBackupCode ? "text" : "numeric"}
            maxLength={useBackupCode ? 32 : 6}
            name="code"
            pattern={useBackupCode ? undefined : "[0-9]{6}"}
            required
          />
        </label>
        <button className="primary" disabled={busy} type="submit">
          {busy ? "Working…" : "Verify"}
        </button>
        <button onClick={() => setUseBackupCode(!useBackupCode)} type="button">
          {useBackupCode ? "Use the authenticator app" : "Use a backup code"}
        </button>
        <p aria-live="polite" className={message ? "status error" : "status"}>
          {message}
        </p>
      </form>
    );
  }

  return (
    <form className="stack" onSubmit={submit}>
      <label>
        Email
        <input autoComplete="email" name="email" required type="email" />
      </label>
      {mode !== "reset" && (
        <label>
          Password
          <input
            autoComplete={
              mode === "signin" ? "current-password" : "new-password"
            }
            minLength={12}
            name="password"
            required
            type="password"
          />
        </label>
      )}
      <button className="primary" disabled={busy} type="submit">
        {busy
          ? "Working…"
          : mode === "signin"
            ? "Sign in"
            : mode === "signup"
              ? "Create account"
              : "Send reset link"}
      </button>
      {mode === "signin" && passkeysEnabled && (
        <button
          disabled={busy}
          onClick={() => void signInWithPasskey()}
          type="button"
        >
          Sign in with a passkey
        </button>
      )}
      <button
        onClick={() => switchMode(mode === "signin" ? "signup" : "signin")}
        type="button"
      >
        {mode === "signin" ? "Create a new account" : "Use an existing account"}
      </button>
      {mode === "signin" && (
        <button onClick={() => switchMode("reset")} type="button">
          Forgot your password?
        </button>
      )}
      <p aria-live="polite" className={message ? "status error" : "status"}>
        {message}
      </p>
    </form>
  );
}
