import Link from "next/link";

import { getOptionalAuthenticatedUser } from "@/lib/auth";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  const user = await getOptionalAuthenticatedUser();
  return (
    <section className="hero">
      <div>
        <p className="eyebrow">Private capture history</p>
        <h1>Your screenshots, wherever you are.</h1>
        <p className="lead">
          XerahS Cloud doesn&apos;t host your files. It keeps a private
          collection of the links to everything you upload with XerahS, so you
          can find any screenshot or recording again from anywhere. Your images
          and videos stay with the destination you uploaded them to.
        </p>
        {user && <p className="session-summary">Signed in as {user.email}</p>}
        <div className="actions">
          <Link className="button primary" href={user ? "/settings" : "/auth"}>
            {user ? "Open your account" : "Sign in"}
          </Link>
          <a
            className="button"
            href="https://github.com/ShareX/XerahS"
            rel="noreferrer"
          >
            Get XerahS
          </a>
        </div>
      </div>
    </section>
  );
}
