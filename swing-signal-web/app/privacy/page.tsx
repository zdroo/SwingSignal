import type { Metadata } from "next";
import Link from "next/link";
import { Lock } from "lucide-react";

export const metadata: Metadata = {
  title: "Privacy Policy",
  description: "What data SwingSignal collects, why, and your rights.",
  alternates: { canonical: "/privacy" },
};

export default function PrivacyPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <div className="mb-8 text-center">
        <Lock className="mx-auto mb-3 h-8 w-8 text-emerald-600 dark:text-emerald-400" />
        <h1 className="text-3xl font-bold text-zinc-900 dark:text-white">Privacy Policy</h1>
        <p className="mt-2 text-sm text-zinc-500">
          Short version: we collect the minimum needed to run the product, we use
          cookie-free analytics, and we never sell your data.
        </p>
      </div>

      <div className="space-y-6 text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">
        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            What we collect
          </h2>
          <ul className="list-disc space-y-1.5 pl-5">
            <li>
              <strong>Account data</strong> — your email address and a securely hashed password
              (we can never see the password itself). If you sign in with Google, we receive your
              email address from Google; we never see your Google password.
            </li>
            <li>
              <strong>Usage data</strong> — which assets you look up on SwingSignal, so we can show
              &quot;most searched&quot; lists and decide what to build next.
            </li>
            <li>
              <strong>Anonymous analytics</strong> — page views and feature usage via a
              privacy-friendly, cookie-free analytics tool. No advertising trackers, no
              fingerprinting, no cross-site tracking.
            </li>
            <li>
              <strong>Waitlist</strong> — if you join the Pro waitlist, we store the email you
              provide, used only to notify you about the Pro launch.
            </li>
          </ul>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            What we do NOT do
          </h2>
          <ul className="list-disc space-y-1.5 pl-5">
            <li>We do not sell or share your data with advertisers or data brokers.</li>
            <li>We do not use advertising cookies (which is why there is no cookie banner).</li>
            <li>We do not send marketing email you didn&apos;t ask for.</li>
          </ul>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            Service providers
          </h2>
          <p>
            We use a small number of processors to run the service: a transactional email provider
            (Resend) to send confirmation and password-reset emails, and hosting infrastructure for
            the application and database. These providers process data only on our instructions.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            Retention & your rights
          </h2>
          <ul className="list-disc space-y-1.5 pl-5">
            <li>Accounts that never confirm their email are automatically deleted after 7 days.</li>
            <li>
              Under the GDPR you can request access to, correction of, or deletion of your personal
              data at any time. Deleting your account removes your email and personal data from our
              systems.
            </li>
            <li>
              Session tokens are stored in your browser&apos;s local storage and can be cleared by
              logging out.
            </li>
          </ul>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">Contact</h2>
          <p>
            For any privacy request, contact us at{" "}
            <a href="mailto:privacy@swingsignal.app" className="text-emerald-600 dark:text-emerald-400 hover:underline">
              privacy@swingsignal.app
            </a>
            . Last updated: July 2026.
          </p>
        </section>
      </div>

      <p className="mt-10 text-center text-xs text-zinc-500">
        See also our <Link href="/terms" className="text-emerald-600 dark:text-emerald-400 hover:underline">Terms of Use</Link> and{" "}
        <Link href="/disclaimer" className="text-emerald-600 dark:text-emerald-400 hover:underline">Disclaimer</Link>.
      </p>
    </div>
  );
}
