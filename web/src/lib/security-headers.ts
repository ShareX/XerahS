const staticHeaders: Readonly<Record<string, string>> = {
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "no-referrer",
  "X-Frame-Options": "DENY",
  "Cross-Origin-Opener-Policy": "same-origin",
  "Permissions-Policy":
    "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), fullscreen=(self), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), picture-in-picture=(), publickey-credentials-create=(self), publickey-credentials-get=(self), usb=()",
};

export function contentSecurityPolicy(nonce: string): string {
  return [
    "default-src 'self'",
    "base-uri 'none'",
    "object-src 'none'",
    "frame-ancestors 'none'",
    "form-action 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'`,
    `style-src 'self' 'nonce-${nonce}'`,
    "img-src 'self' data: blob: https:",
    "media-src 'self' blob: https:",
    "font-src 'self'",
    // Auth and data are same-origin (/api/auth, /api/v1).
    "connect-src 'self'",
    "worker-src 'self' blob:",
    "manifest-src 'self'",
    "upgrade-insecure-requests",
  ].join("; ");
}

export function applySecurityHeaders(
  headers: Headers,
  nonce: string,
  production: boolean,
): void {
  for (const [name, value] of Object.entries(staticHeaders))
    headers.set(name, value);
  headers.set(
    production
      ? "Content-Security-Policy"
      : "Content-Security-Policy-Report-Only",
    contentSecurityPolicy(nonce),
  );
  if (production)
    headers.set(
      "Strict-Transport-Security",
      "max-age=31536000; includeSubDomains",
    );
}

export function applyNoStore(headers: Headers): void {
  headers.set("Cache-Control", "private, no-store, max-age=0");
  headers.set("Pragma", "no-cache");
  headers.append("Vary", "Cookie, Authorization");
}

export function isPersonalizedPath(pathname: string): boolean {
  return (
    pathname === "/" ||
    /^\/(?:api|auth|oauth|settings)(?:\/|$)/.test(pathname) ||
    /^\/[^/]+\/?$/.test(pathname)
  );
}
