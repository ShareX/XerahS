import type { Metadata } from "next";
import Link from "next/link";

import { getOptionalAuthenticatedUser } from "@/lib/auth";
import { LEGAL_ENTITY, LEGAL_LINKS } from "@/lib/legal";

import "./globals.css";

export const metadata: Metadata = {
  title: { default: "XerahS Cloud", template: "%s · XerahS Cloud" },
  description: "Your private, owner-only collection of XerahS upload links.",
  robots: { index: false, follow: false, nocache: true },
  referrer: "no-referrer",
};

export const dynamic = "force-dynamic";

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const user = await getOptionalAuthenticatedUser();
  return (
    <html lang="en">
      <body>
        <header className="shell topbar">
          <Link className="brand" href="/">
            XerahS Cloud
          </Link>
          <nav className="topnav" aria-label="Primary navigation">
            {user ? (
              <>
                <span
                  aria-label={`Signed in as ${user.email}`}
                  className="session-status"
                  title={user.email}
                >
                  Signed in
                </span>
                <Link className="button primary" href="/settings">
                  Account
                </Link>
              </>
            ) : (
              <Link className="button primary" href="/auth">
                Sign in
              </Link>
            )}
          </nav>
        </header>
        <main className="shell main">{children}</main>
        <footer className="shell site-footer">
          <nav aria-label="Legal">
            {LEGAL_LINKS.map((link) => (
              <Link key={link.href} href={link.href}>
                {link.label}
              </Link>
            ))}
          </nav>
          <span>{LEGAL_ENTITY}</span>
        </footer>
      </body>
    </html>
  );
}
