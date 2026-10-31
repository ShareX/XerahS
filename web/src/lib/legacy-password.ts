// Password verification for accounts imported from the Supabase-hosted Cloud.
//
// Supabase Auth stored bcrypt hashes ("$2a$…"); Better Auth hashes with scrypt.
// Imported accounts keep their bcrypt hash until the password is next changed
// or reset, which stores a scrypt hash. Everything else goes to Better Auth.
//
// Kept free of `@/` imports so the Better Auth CLI can load it.

import { verifyPassword } from "better-auth/crypto";
import bcrypt from "bcryptjs";

const bcryptHash = /^\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}$/;

export function isBcryptHash(hash: string): boolean {
  return bcryptHash.test(hash);
}

export async function verifyPasswordWithLegacy({
  hash,
  password,
}: {
  hash: string;
  password: string;
}): Promise<boolean> {
  if (isBcryptHash(hash)) return bcrypt.compare(password, hash);
  return verifyPassword({ hash, password });
}
