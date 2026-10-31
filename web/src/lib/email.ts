// Transactional email for Better Auth (verification, password reset).
// Production sends through Resend's HTTP API; development prints the message
// so the link can be followed locally. Kept free of `@/` imports so the
// Better Auth CLI can load it.

export interface EmailMessage {
  to: string;
  subject: string;
  text: string;
}

export async function sendEmail(message: EmailMessage): Promise<void> {
  const apiKey = process.env.RESEND_API_KEY;
  const from = process.env.EMAIL_FROM;
  if (!apiKey || !from) {
    if (
      process.env.APP_ENV === "production" ||
      process.env.APP_ENV === "staging"
    ) {
      throw new Error(
        "Email delivery is not configured (RESEND_API_KEY, EMAIL_FROM).",
      );
    }
    console.info("email_not_sent_development", {
      to: message.to,
      subject: message.subject,
      text: message.text,
    });
    return;
  }

  const response = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      from,
      to: [message.to],
      subject: message.subject,
      text: message.text,
    }),
    signal: AbortSignal.timeout(10_000),
  });
  if (!response.ok) {
    throw new Error(`Email delivery failed with HTTP ${response.status}.`);
  }
}
