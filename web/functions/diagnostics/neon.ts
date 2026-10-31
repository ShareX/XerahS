import { defineConfig } from "@neon/config/v1";

export default defineConfig({
  functions: {
    // Crash-report ingest for the desktop Debug tab (functions/diagnostics).
    diagnostics: {
      name: "XerahS diagnostics ingest",
      source: "index.ts",
      env: {
        // HMAC key for per-network rate limits; client IPs are never stored.
        DIAGNOSTICS_NETWORK_SECRET: process.env.DIAGNOSTICS_NETWORK_SECRET!,
      },
    },
  },
  triggers: {
    // Nightly partitions + space reclamation (diagnostics.run_maintenance).
    "diagnostics-maintenance": {
      type: "schedule",
      function: "diagnostics",
      cron: "17 18 * * *", // 18:17 UTC = 02:17 in Perth
      functionPath: "/v1/internal/maintenance",
    },
  },
});
