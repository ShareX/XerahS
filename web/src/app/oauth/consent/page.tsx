import Link from "next/link";
import { redirect } from "next/navigation";

import { ConsentDecisionForm } from "@/components/consent-decision-form";
import { MfaControls } from "@/components/mfa-controls";
import { requireAuthenticatedUser } from "@/lib/auth";
import { API_AUDIENCE, DESKTOP_CLIENT_ID } from "@/lib/better-auth";
import { getPublicEnv, getServerEnv } from "@/lib/env";
import { ApiError } from "@/lib/errors";
import {
  desktopOAuthRedirectUris,
  parseDesktopAuthorizationQuery,
} from "@/lib/oauth-validation";

export const dynamic = "force-dynamic";

interface ConsentPageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

function ConsentError({ children }: { children: React.ReactNode }) {
  return (
    <section className="card auth-card">
      <p className="eyebrow">Desktop authorization</p>
      <h2>Authorization unavailable</h2>
      <p className="lead">{children}</p>
      <Link className="button" href="/settings">
        Return to settings
      </Link>
    </section>
  );
}

function toSearchParams(
  input: Record<string, string | string[] | undefined>,
): URLSearchParams {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(input)) {
    for (const item of Array.isArray(value)
      ? value
      : value === undefined
        ? []
        : [value])
      params.append(key, item);
  }
  return params;
}

export default async function ConsentPage({ searchParams }: ConsentPageProps) {
  const query = toSearchParams(await searchParams);
  let request: ReturnType<typeof parseDesktopAuthorizationQuery>;
  try {
    request = parseDesktopAuthorizationQuery(query, {
      clientId: DESKTOP_CLIENT_ID,
      redirectUris: desktopOAuthRedirectUris(getServerEnv().APP_ORIGIN),
      audience: API_AUDIENCE,
    });
  } catch {
    return (
      <ConsentError>
        The authorization request is invalid or has expired. Start again from
        XerahS.
      </ConsentError>
    );
  }

  const consentPath = `/oauth/consent?${request.oauthQuery}`;
  let user: Awaited<ReturnType<typeof requireAuthenticatedUser>>;
  try {
    user = await requireAuthenticatedUser(undefined, { verifiedEmail: true });
  } catch (error) {
    if (error instanceof ApiError && error.status === 401)
      redirect(`/auth?next=${encodeURIComponent(consentPath)}`);
    return (
      <ConsentError>
        Sign in with a verified email address before authorizing the desktop
        application.
      </ConsentError>
    );
  }

  if (user.aal !== "aal2") {
    return (
      <section className="consent-stack">
        <article className="card">
          <p className="eyebrow">Desktop authorization</p>
          <h2>Strong authentication required</h2>
          <p className="lead">
            Verify your authenticator before reviewing this authorization
            request. This page will resume automatically after verification.
          </p>
        </article>
        <MfaControls
          passkeysEnabled={getPublicEnv().NEXT_PUBLIC_PASSKEYS_ENABLED}
          strongAuth={false}
        />
      </section>
    );
  }

  return (
    <section className="card auth-card stack">
      <p className="eyebrow">Desktop authorization</p>
      <h2>Connect XerahS desktop</h2>
      <p className="lead">
        Allow the XerahS desktop application to use your owner-only cloud
        gallery as {user.email}?
      </p>
      <div>
        <strong>Requested access</strong>
        <ul>
          {request.scopes.map((scope) => (
            <li key={scope}>{scope}</li>
          ))}
        </ul>
      </div>
      <p className="status">
        The desktop application can act with the same owner permissions as this
        session until you sign out everywhere. Approve only if you started this
        request from XerahS.
      </p>
      <ConsentDecisionForm oauthQuery={request.oauthQuery} />
    </section>
  );
}
