# XerahS Cloud web

Owner-only screenshot and screencast gallery for XIP0085. This is a Next.js 16 App Router application deployed to Vercel Sydney (`syd1`) with self-hosted Better Auth on Neon Postgres (`xerahs-cloud`, `aws-ap-southeast-2`), hosted Stripe Billing, and a private Cloudflare R2 operations ledger.

## Local development

Requirements: Node.js 24 and Corepack. From `web/`:

```powershell
corepack prepare pnpm@10.28.2 --activate
Copy-Item .env.example .env.local
pnpm install --frozen-lockfile
pnpm dev
```

Run `neon link` (or `neon env pull`) in `web/` to write the Neon connection strings into `.env.local`, then add `BETTER_AUTH_SECRET`. Without `RESEND_API_KEY`/`EMAIL_FROM`, development prints verification and reset links to the server log. Use a Neon branch, never `main`, for local work. Stripe and R2 can remain unavailable while working on non-billing UI; `LEDGER_USE_LOCAL_FAKE=true` is development/Preview only. Production startup fails closed when privileged configuration is incomplete or the fake ledger is enabled.

The project uses TypeScript 7's native `tsc` for application type checking and Next.js production builds. TypeScript 7.0 does not expose the compiler API used by `typescript-eslint`, so the ESLint toolchain is isolated in `tooling/eslint` with Microsoft's official `@typescript/typescript6` compatibility package. This separation keeps the application compiler on TypeScript 7 without running ESLint against an unsupported API.

## Verification

```powershell
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

`pnpm audit --prod --audit-level high` is the first gate of the `XerahS Cloud Web CI` web job and fails the whole job when it trips. Fix a failing advisory by upgrading the affected package to a patched release; never lower `--audit-level` and never add an audit ignore or an override that keeps a vulnerable version in place. When an advisory has no patched release yet, record the package, the advisory id and the mitigation in this file, next to the lockfile, instead of skipping the gate.

Dependency refresh, 2026-10-25, after CI run 40038453939 (21 findings: 2 low, 11 moderate, 6 high, 2 critical):

- `next` 16.3.7 → 16.4.3 fixes GHSA-2464-fhr4-w9xw and GHSA-673q-mjm6-r5xr (unauthenticated remote code execution in the image optimization API and in `use cache`), GHSA-p3m5-59mp-jv5r (denial of service in App Router server actions) and GHSA-cjq9-62q9-8jv4 (server-side request forgery in image optimization), plus the 16.3.x cache advisories.
- `zod` 4.4.3 and 4.6.5 → 4.6.7 fixes GHSA-mrhx-c7xc-m3w5 (memory exhaustion when validating oversized constrained collections). One `zod` version now serves the whole production graph.
- `vitest` 4.1.11 → 4.1.12 fixes GHSA-qxg6-89mg-rqv8 and GHSA-xp26-3454-mjgp in `@vitest/mocker`, and pulls `vite` 8.2.2 → 8.3.5 (GHSA-587h-7wmj-h5pm) and `source-map-js` 1.2.1 → 1.2.2 (GHSA-68fv-2mgg-jv7q).
- `eslint-config-next` 16.3.2 → 16.4.3 keeps the lint configuration in lockstep with `next`.

`vite` and `source-map-js` only reached the production audit because `better-auth` declares `vitest` as an optional peer dependency, so the workspace's development `vitest` sits inside the production dependency graph that `pnpm audit --prod` walks. `pnpm audit --prod` is clean after the refresh. A full `pnpm audit` still reports development-only findings in `wrangler` → `miniflare` → `undici`/`sharp` and in the ESLint toolchain under `tooling/eslint`; those are outside the production gate and are tracked separately.

Database migrations, RLS policies, the local seed and pgTAP tests live in `db/cloud/`. `00000000000000_supabase_compat.sql` recreates the small part of Supabase the XIP0085 SQL uses (the `anon`/`authenticated`/`service_role` roles and `auth.uid()`/`auth.jwt()` over `request.jwt.claims`), so those migrations run unchanged on plain Postgres. Apply migrations with `node scripts/db-migrate.ts --dir db/cloud/migrations`, never by editing the database by hand; test them on a throwaway Neon branch first. The web handlers depend on the narrowly granted RPCs documented in `openapi.yaml` and the migrations; `src/lib/database.ts` calls them the way PostgREST did (`SET LOCAL ROLE`, request claims, then the function).

The root workflows run the Node 24 application checks and a Postgres 17 migration, seed and pgTAP suite. Staging deployment is manual and protected until the Vercel and Neon environment secrets are connected.

## Security model

- Every owner/API response is dynamic and `private, no-store`.
- `src/proxy.ts` emits a per-request CSP nonce; auth and data are same-origin, so `connect-src` is `'self'`.
- Better Auth (`src/lib/better-auth.ts`) handles email/password with verified email, TOTP two-factor with backup codes, passkeys, and the desktop OAuth 2.1 server (authorization code + PKCE, ES256 access tokens for `/api/v1`). Its users and sessions are mirrored into `auth.users`/`auth.sessions` by triggers; a session is `aal2` only after a second factor or passkey is verified. Each desktop authorization is its own revocable session, so signing out everywhere also cuts off the desktop.
- Owner data requires a current `aal2` session. Email verification and recent strong authentication are checked for trial and billing operations.
- Cookie-authenticated mutations require the exact configured `Origin`; desktop bearer requests are not CORS-enabled.
- Publish accepts public HTTPS metadata only, derives titles from a leaf filename, strips URL fragments, and never server-fetches media.
- Stripe Checkout and Portal use fixed server configuration. The webhook validates the raw body, signature, mode, and event allowlist before a transactional database hook.
- Trial grants and deletion tombstones are written through a durable Postgres outbox. R2 writes use `If-None-Match: *`, `Content-MD5`, canonical SHA-256/HMAC envelopes, and fail closed on collisions.

Do not enable Stripe Tax until the operating entity has approved registrations and a product tax code. Do not place database, Better Auth, Stripe, R2, HMAC, webhook, email, or cron secrets in `NEXT_PUBLIC_*` variables.

## Provider setup

1. Use the Neon project `xerahs-cloud` in Sydney: `main` for production and a protected child branch for staging. Apply the reviewed migrations with `scripts/db-migrate.ts`, then register the desktop OAuth client with `node scripts/register-desktop-oauth-client.ts`. Configure Resend (or another sender) for `EMAIL_FROM`.
2. Create one Stripe Product with separate test/live monthly and annual Prices. Configure a restricted Customer Portal and signed environment-specific webhook at `/api/webhooks/stripe`.
3. Enable R2 billing, then run `node scripts/configure-cloudflare-r2.mjs` with a protected administration token and environment-specific `R2_BUCKET`. The checked-in desired state creates a private Standard/APAC bucket, disables `r2.dev`, applies the indefinite trial lock, and applies matching 180-day deletion lock/lifecycle rules. Runtime uses a separate bucket-scoped Object Read & Write token.
4. Configure Vercel environments independently, keep Preview protected, and deploy production only through protected CI.
5. Deploy `infrastructure/cloudflare/scheduler` with the same `CRON_SECRET` as Vercel (`wrangler deploy --env staging` or `wrangler deploy --env production`). Its Cron Triggers invoke the authenticated ledger-dispatch, account-deletion, and Stripe-reconciliation routes without requiring a Vercel Pro cron plan. The Worker is an outbound scheduler only and is not a proxy for application traffic.
6. Point Cloudflare DNS-only records to Vercel only after the production launch gates in XIP0085 are complete.

R2 enablement, paid provider plans, live billing, tax, production DNS, and traffic require the explicit cost/legal/tax/launch approvals in XIP0085. Once approved, the checked-in scripts and protected workflows apply and continuously verify the desired state.
