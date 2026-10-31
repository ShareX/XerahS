import Link from "next/link";

import { Gallery } from "@/components/gallery";
import { createUserDatabaseClient, requireAuthenticatedUser } from "@/lib/auth";
import { getAccountSummary, rpc, type GalleryItem } from "@/lib/database";
import { ApiError } from "@/lib/errors";

export const dynamic = "force-dynamic";

interface PageProps {
  params: Promise<{ slug: string }>;
}

interface ProfileData {
  summary: Awaited<ReturnType<typeof getAccountSummary>>;
  page: { items: GalleryItem[]; nextCursor: string | null };
}

type ProfileState =
  | { kind: "ready"; data: ProfileData }
  | { kind: "anonymous" }
  | { kind: "strong-auth-required" };

function SignInWall() {
  return (
    <section className="card sign-wall">
      <p className="eyebrow">Private gallery</p>
      <h2>Owner sign-in required</h2>
      <p className="lead">
        Sign in with the account that owns this profile and complete strong
        authentication.
      </p>
      <Link className="button primary" href="/auth">
        Sign in
      </Link>
    </section>
  );
}

function StrongAuthWall() {
  return (
    <section className="card sign-wall">
      <p className="eyebrow">Additional verification required</p>
      <h2>Complete two-factor authentication</h2>
      <p className="lead">
        Your account is signed in. Complete an authenticator challenge to open
        this private gallery.
      </p>
      <Link className="button primary" href="/settings">
        Continue in account settings
      </Link>
    </section>
  );
}

function GalleryUnavailableWall() {
  return (
    <section className="card sign-wall">
      <p className="eyebrow">Private gallery</p>
      <h2>Gallery not available</h2>
      <p className="lead">
        You are signed in, but this address does not belong to your account.
      </p>
      <Link className="button primary" href="/settings">
        Open your account
      </Link>
    </section>
  );
}

async function loadProfileData(): Promise<ProfileState> {
  try {
    await requireAuthenticatedUser(undefined, { strong: true });
    const client = await createUserDatabaseClient();
    const [summary, page] = await Promise.all([
      getAccountSummary(client),
      rpc<{ items: GalleryItem[]; nextCursor: string | null }>(
        client,
        "list_my_gallery_items",
        { p_limit: 50 },
      ),
    ]);
    return { kind: "ready", data: { summary, page } };
  } catch (error) {
    if (error instanceof ApiError) {
      if (error.status === 401 && error.code === "authentication_required") {
        return { kind: "anonymous" };
      }
      if (error.status === 403 && error.code === "strong_auth_required") {
        return { kind: "strong-auth-required" };
      }
    }
    throw error;
  }
}

export default async function ProfilePage({ params }: PageProps) {
  const [state, { slug }] = await Promise.all([loadProfileData(), params]);
  if (state.kind === "anonymous") return <SignInWall />;
  if (state.kind === "strong-auth-required") return <StrongAuthWall />;

  const { summary, page } = state.data;
  if (summary.slug.toLowerCase() !== slug.toLowerCase())
    return <GalleryUnavailableWall />;

  return (
    <Gallery
      initialItems={page.items}
      initialNextCursor={page.nextCursor}
      slug={summary.slug}
      timeZone={summary.timeZone}
    />
  );
}
