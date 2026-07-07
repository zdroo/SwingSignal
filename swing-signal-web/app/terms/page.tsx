import type { Metadata } from "next";
import Link from "next/link";
import { ScrollText } from "lucide-react";

export const metadata: Metadata = {
  title: "Terms of Use — SwingSignal",
  description: "The rules for using SwingSignal.",
};

export default function TermsPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <div className="mb-8 text-center">
        <ScrollText className="mx-auto mb-3 h-8 w-8 text-emerald-600 dark:text-emerald-400" />
        <h1 className="text-3xl font-bold text-zinc-900 dark:text-white">Terms of Use</h1>
      </div>

      <div className="space-y-6 text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">
        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            1. The service
          </h2>
          <p>
            SwingSignal provides historical macroeconomic context and statistics about financial
            assets. By using the site you accept these terms. If you do not accept them, do not use
            the service. The service is provided &quot;as is&quot;, without warranties of any kind —
            including availability, accuracy, or fitness for a particular purpose.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            2. No financial advice
          </h2>
          <p>
            Nothing on SwingSignal constitutes financial, investment, legal, or tax advice. See the
            full <Link href="/disclaimer" className="text-emerald-600 dark:text-emerald-400 hover:underline">Disclaimer</Link>.
            You are solely responsible for your investment decisions and any resulting gains or losses.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            3. Accounts
          </h2>
          <ul className="list-disc space-y-1.5 pl-5">
            <li>You must provide a valid email address and keep your credentials secure.</li>
            <li>One account per person. Accounts are for personal, non-commercial use.</li>
            <li>Unconfirmed accounts are deleted automatically after 7 days.</li>
            <li>You may delete your account at any time by contacting us.</li>
          </ul>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            4. Acceptable use
          </h2>
          <p>You agree not to:</p>
          <ul className="mt-2 list-disc space-y-1.5 pl-5">
            <li>Scrape, bulk-download, or programmatically harvest data from the service;</li>
            <li>Circumvent rate limits, access controls, or account tiers;</li>
            <li>Resell or redistribute our data or analysis without written permission;</li>
            <li>Use the service for any unlawful purpose.</li>
          </ul>
          <p className="mt-2">
            We may suspend or terminate accounts that violate these rules.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            5. Limitation of liability
          </h2>
          <p>
            To the maximum extent permitted by law, SwingSignal and its operator are not liable for
            any direct, indirect, incidental, or consequential damages arising from your use of the
            service — including trading losses, data inaccuracies, or service interruptions.
          </p>
        </section>

        <section>
          <h2 className="mb-2 text-lg font-semibold text-zinc-900 dark:text-white">
            6. Changes
          </h2>
          <p>
            We may update these terms as the product evolves. Material changes will be announced on
            the site. Continued use after changes constitutes acceptance. Last updated: July 2026.
          </p>
        </section>
      </div>

      <p className="mt-10 text-center text-xs text-zinc-500">
        See also our <Link href="/privacy" className="text-emerald-600 dark:text-emerald-400 hover:underline">Privacy Policy</Link> and{" "}
        <Link href="/disclaimer" className="text-emerald-600 dark:text-emerald-400 hover:underline">Disclaimer</Link>.
      </p>
    </div>
  );
}
