import "server-only";

import { z } from "zod";

const booleanString = z
  .enum(["true", "false"])
  .transform((value) => value === "true");

const serverSchema = z.object({
  APP_ENV: z
    .enum(["development", "preview", "staging", "production"])
    .default("development"),
  APP_ORIGIN: z.url().default("http://localhost:3000"),
  XERAHS_DESKTOP_OAUTH_CLIENT_ID: z.uuid().optional(),
  DATABASE_URL: z
    .string()
    .regex(/^postgres(?:ql)?:\/\//)
    .optional(),
  BETTER_AUTH_SECRET: z.string().min(32).optional(),
  RESEND_API_KEY: z.string().min(8).optional(),
  EMAIL_FROM: z.string().min(3).optional(),
  STRIPE_SECRET_KEY: z.string().min(8).optional(),
  STRIPE_WEBHOOK_SECRET: z.string().startsWith("whsec_").optional(),
  STRIPE_PRICE_MONTHLY: z.string().startsWith("price_").optional(),
  STRIPE_PRICE_ANNUAL: z.string().startsWith("price_").optional(),
  STRIPE_ENTITLED_LEGACY_PRICES: z.string().default(""),
  STRIPE_PORTAL_CONFIGURATION_ID: z.string().startsWith("bpc_").optional(),
  STRIPE_EXPECT_LIVEMODE: booleanString.default(false),
  STRIPE_TAX_ENABLED: booleanString.default(false),
  R2_LEDGER_ACCOUNT_ID: z.string().optional(),
  R2_LEDGER_BUCKET: z.string().optional(),
  R2_LEDGER_ACCESS_KEY_ID: z.string().optional(),
  R2_LEDGER_SECRET_ACCESS_KEY: z.string().optional(),
  R2_LEDGER_READ_ACCESS_KEY_ID: z.string().optional(),
  R2_LEDGER_READ_SECRET_ACCESS_KEY: z.string().optional(),
  LEDGER_HMAC_ACTIVE_VERSION: z
    .string()
    .regex(/^v\d+$/)
    .default("v1"),
  LEDGER_HMAC_SECRET_V1: z.string().min(32).optional(),
  LEDGER_HMAC_SECRETS_JSON: z.string().optional(),
  IDENTITY_HMAC_SECRET_V1: z.string().min(32).optional(),
  RECOVERY_CODE_PEPPER_V1: z.string().min(32).optional(),
  LEDGER_USE_LOCAL_FAKE: booleanString.default(true),
  CRON_SECRET: z.string().min(32).optional(),
});

const publicSchema = z.object({
  NEXT_PUBLIC_PASSKEYS_ENABLED: booleanString.default(false),
});

export type ServerEnv = z.infer<typeof serverSchema>;
export type PublicEnv = z.infer<typeof publicSchema>;

let cachedServer: ServerEnv | undefined;
let cachedPublic: PublicEnv | undefined;

export function getServerEnv(): ServerEnv {
  cachedServer ??= serverSchema.parse({
    ...process.env,
    // Vercel supplies VERCEL_ENV. Falling back to it prevents a production
    // deployment with a missing APP_ENV from silently using development
    // defaults and bypassing the production secret checks.
    APP_ENV: process.env.APP_ENV ?? process.env.VERCEL_ENV,
  });
  return cachedServer;
}

export function getPublicEnv(): PublicEnv {
  cachedPublic ??= publicSchema.parse({
    NEXT_PUBLIC_PASSKEYS_ENABLED: process.env.NEXT_PUBLIC_PASSKEYS_ENABLED,
  });
  return cachedPublic;
}

export function assertProductionConfiguration(): void {
  const env = getServerEnv();
  if (env.APP_ENV !== "production" && env.APP_ENV !== "staging") return;

  const required: Array<keyof ServerEnv> = [
    "DATABASE_URL",
    "BETTER_AUTH_SECRET",
    "RESEND_API_KEY",
    "EMAIL_FROM",
    "XERAHS_DESKTOP_OAUTH_CLIENT_ID",
    "STRIPE_SECRET_KEY",
    "STRIPE_WEBHOOK_SECRET",
    "STRIPE_PRICE_MONTHLY",
    "STRIPE_PRICE_ANNUAL",
    "STRIPE_PORTAL_CONFIGURATION_ID",
    "R2_LEDGER_ACCOUNT_ID",
    "R2_LEDGER_BUCKET",
    "R2_LEDGER_ACCESS_KEY_ID",
    "R2_LEDGER_SECRET_ACCESS_KEY",
    "LEDGER_HMAC_SECRET_V1",
    "IDENTITY_HMAC_SECRET_V1",
    "RECOVERY_CODE_PEPPER_V1",
    "CRON_SECRET",
  ];
  const missing = required.filter((name) => !env[name]);
  const origin = new URL(env.APP_ORIGIN);
  const expectedHost = "cloud.xerahs.com";
  const invalidMode =
    (env.APP_ENV === "production" && !env.STRIPE_EXPECT_LIVEMODE) ||
    (env.APP_ENV === "staging" && env.STRIPE_EXPECT_LIVEMODE);
  const invalidOrigin =
    origin.protocol !== "https:" ||
    origin.hostname !== expectedHost ||
    origin.username !== "" ||
    origin.password !== "" ||
    origin.pathname !== "/" ||
    origin.search !== "" ||
    origin.hash !== "";
  if (
    missing.length > 0 ||
    env.LEDGER_USE_LOCAL_FAKE ||
    invalidMode ||
    invalidOrigin
  ) {
    throw new Error(
      `${env.APP_ENV} configuration is invalid: ${missing.join(", ") || "mode, origin, or ledger storage"}`,
    );
  }
}
