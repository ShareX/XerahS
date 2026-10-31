/**
 * Same-origin URL Better Auth asks the client to continue to after a sign-in
 * step completes an OAuth authorize request (`{ redirect: true, url }`), or
 * null when the response is a plain sign-in or points elsewhere.
 */
export function oauthContinueUrl(data: unknown, origin: string): string | null {
  if (!data || typeof data !== "object") return null;
  const { redirect, url } = data as { redirect?: unknown; url?: unknown };
  if (redirect !== true || typeof url !== "string") return null;
  try {
    const target = new URL(url, origin);
    return target.origin === new URL(origin).origin ? target.href : null;
  } catch {
    return null;
  }
}
