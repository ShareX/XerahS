import { NextResponse, type NextRequest } from "next/server";

import {
  applyNoStore,
  applySecurityHeaders,
  isPersonalizedPath,
} from "@/lib/security-headers";

function nonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(18));
  return btoa(String.fromCharCode(...bytes));
}

export async function proxy(request: NextRequest) {
  const cspNonce = nonce();
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", cspNonce);
  const response = NextResponse.next({ request: { headers: requestHeaders } });
  applySecurityHeaders(
    response.headers,
    cspNonce,
    process.env.APP_ENV === "production",
  );
  if (isPersonalizedPath(request.nextUrl.pathname)) {
    applyNoStore(response.headers);
  }
  return response;
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp|avif)$).*)",
  ],
};
