import { defineConfig } from "@neon/config/v1";

// XerahS Cloud (project xerahs-cloud, aws-ap-southeast-2). Postgres only:
// auth is self-hosted Better Auth inside the Next.js app. The diagnostics
// ingest Function has its own policy in functions/diagnostics/neon.ts.
export default defineConfig({});
