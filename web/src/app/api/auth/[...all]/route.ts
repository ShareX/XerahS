import { toNextJsHandler } from "better-auth/next-js";

import { auth } from "@/lib/better-auth";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// Sign-in, sign-up, email verification, two-factor, passkeys, JWKS and the
// desktop OAuth server (/api/auth/oauth2/*).
export const { GET, POST } = toNextJsHandler(auth);
