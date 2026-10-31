import { ResetPasswordForm } from "@/components/reset-password-form";

export const dynamic = "force-dynamic";

export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<{
    token?: string | string[];
    error?: string | string[];
  }>;
}) {
  const input = await searchParams;
  const token =
    typeof input.token === "string" &&
    /^[A-Za-z0-9_-]{8,256}$/.test(input.token)
      ? input.token
      : null;
  return (
    <section className="card auth-card">
      <p className="eyebrow">Owner access</p>
      <h2>Choose a new password</h2>
      {token ? (
        <ResetPasswordForm token={token} />
      ) : (
        <p className="lead">
          This reset link is invalid or has expired. Request a new one from the
          sign-in page.
        </p>
      )}
    </section>
  );
}
