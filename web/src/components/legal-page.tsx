import Link from "next/link";

import { getPublicEnv } from "@/lib/env";
import { LEGAL_ENTITY, LEGAL_LINKS, LEGAL_VERSION } from "@/lib/legal";

export function LegalPage({
  title,
  children,
}: Readonly<{ title: string; children: React.ReactNode }>) {
  const supportEmail = getPublicEnv().NEXT_PUBLIC_SUPPORT_EMAIL;
  return (
    <article className="card stack legal">
      <p className="eyebrow">XerahS Cloud</p>
      <h1 className="legal-title">{title}</h1>
      <p className="status">
        Provided by {LEGAL_ENTITY}. Version {LEGAL_VERSION}.
      </p>
      {children}
      <h2>Contact</h2>
      <p>
        {supportEmail ? (
          <>
            Questions about these terms or your account:{" "}
            <a href={`mailto:${supportEmail}`}>{supportEmail}</a>.
          </>
        ) : (
          "Questions about these terms or your account can be raised from your account settings page."
        )}
      </p>
      <nav aria-label="Legal documents" className="actions">
        {LEGAL_LINKS.map((link) => (
          <Link key={link.href} href={link.href}>
            {link.label}
          </Link>
        ))}
      </nav>
    </article>
  );
}
