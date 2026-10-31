import bcrypt from "bcryptjs";
import { hashPassword } from "better-auth/crypto";
import { describe, expect, it } from "vitest";

import { isBcryptHash, verifyPasswordWithLegacy } from "./legacy-password";

describe("verifyPasswordWithLegacy", () => {
  it("verifies Supabase-era bcrypt hashes", async () => {
    const hash = await bcrypt.hash("correct horse battery", 4);
    expect(isBcryptHash(hash)).toBe(true);
    expect(
      await verifyPasswordWithLegacy({
        hash,
        password: "correct horse battery",
      }),
    ).toBe(true);
    expect(
      await verifyPasswordWithLegacy({ hash, password: "wrong horse battery" }),
    ).toBe(false);
  });

  it("leaves Better Auth scrypt hashes to Better Auth", async () => {
    const hash = await hashPassword("correct horse battery");
    expect(isBcryptHash(hash)).toBe(false);
    expect(
      await verifyPasswordWithLegacy({
        hash,
        password: "correct horse battery",
      }),
    ).toBe(true);
    expect(
      await verifyPasswordWithLegacy({ hash, password: "wrong horse battery" }),
    ).toBe(false);
  });

  it("does not treat malformed hashes as bcrypt", () => {
    expect(isBcryptHash("$2a$10$short")).toBe(false);
    expect(isBcryptHash("")).toBe(false);
  });
});
