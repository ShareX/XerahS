import Link from "next/link";

import { MfaControls } from "@/components/mfa-controls";
import { ProfileSetup } from "@/components/profile-setup";
import { SettingsControls } from "@/components/settings-controls";
import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { getAccountSummary } from "@/lib/database";
import { getPublicEnv, getServerEnv } from "@/lib/env";
import { ApiError } from "@/lib/errors";

export const dynamic = "force-dynamic";

type SettingsData = {
  user: Awaited<ReturnType<typeof requireAuthenticatedUser>>;
  summary: Awaited<ReturnType<typeof getAccountSummary>> | null;
};

async function loadSettingsData(): Promise<SettingsData | null> {
  try {
    const user = await requireAuthenticatedUser();
    let summary: Awaited<ReturnType<typeof getAccountSummary>> | null = null;
    try {
      summary = await getAccountSummary(await createUserDatabaseClient());
    } catch {
      // New verified users have no application profile until onboarding completes.
    }
    return { user, summary };
  } catch (error) {
    if (
      error instanceof ApiError &&
      error.status === 401 &&
      error.code === "authentication_required"
    ) {
      return null;
    }
    throw error;
  }
}

export default async function SettingsPage() {
  const data = await loadSettingsData();
  if (!data) {
    return (
      <section className="card sign-wall">
        <h2>Sign in required</h2>
        <p className="lead">Sign in to configure XerahS Cloud.</p>
        <Link className="button primary" href="/auth">
          Sign in
        </Link>
      </section>
    );
  }

  const { user, summary } = data;
  const profileHost = new URL(getServerEnv().APP_ORIGIN).host;
  return (
    <section>
      <p className="eyebrow">XerahS Cloud</p>
      <h1>Settings</h1>
      <div className="settings-grid">
        <article className="card">
          <h2>Profile</h2>
          <p>{user.email}</p>
          {summary && (
            <p>
              <Link href={`/${summary.slug}`}>
                {profileHost}/{summary.slug}
              </Link>
            </p>
          )}
        </article>
        <article className="card">
          <h2>Access</h2>
          <p>
            Strong authentication:{" "}
            {user.aal === "aal2" ? "Complete" : "Challenge required"}
          </p>
          <p>
            Publishing:{" "}
            {summary?.canPublish
              ? "Available"
              : "Trial or subscription required"}
          </p>
        </article>
        <article className="card">
          <h2>Subscription</h2>
          <p>
            Trial:{" "}
            {summary?.trialStatus.replaceAll("_", " ") ?? "Profile required"}
          </p>
          <p>Paid status: {summary?.subscriptionStatus ?? "None"}</p>
        </article>
      </div>
      <MfaControls
        passkeysEnabled={getPublicEnv().NEXT_PUBLIC_PASSKEYS_ENABLED}
        strongAuth={user.aal === "aal2"}
      />
      {!summary && user.aal === "aal2" && <ProfileSetup />}
      {summary && (
        <SettingsControls
          strongAuth={user.aal === "aal2"}
          trialStatus={summary.trialStatus}
        />
      )}
    </section>
  );
}
