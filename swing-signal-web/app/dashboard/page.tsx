import type { Metadata } from "next";
import { api } from "@/lib/api";

export const metadata: Metadata = {
  title: "Live Macro Dashboard",
  description:
    "Today's macro regime at a glance: analyze any asset, see the most similar historical periods, and get the picture in plain English.",
  alternates: { canonical: "/dashboard" },
};
import { AssetSearch } from "@/components/AssetSearch";
import { InfoTip } from "@/components/InfoTip";
import { PopularAssets } from "@/components/PopularAssets";
import { ProWaitlist } from "@/components/ProWaitlist";
import { signalSeverity, summarizeRegime, MARKET_MOVER_THRESHOLD } from "@/lib/regime-insight";
import type { MacroRegimeDto } from "@/types";
import { AlertCircle, ScrollText } from "lucide-react";
import { LiveMacroCta } from "@/components/LiveMacroCta";

async function getPageData() {
  try {
    const [regime, matches, popular] = await Promise.all([
      api.getCurrentRegime(),
      api.getHistoricalMatches(10),
      api.getPopularAssets().catch(() => []),
    ]);
    return { regime, matches, popular, error: null };
  } catch {
    return {
      regime: null,
      matches: [],
      popular: [],
      error: "Could not reach the SwingSignal API. Make sure the backend is running.",
    };
  }
}

export default async function DashboardPage() {
  const { regime, matches, popular, error } = await getPageData();

  if (error) {
    return (
      <div className="mx-auto max-w-7xl px-4 py-16 text-center">
        <AlertCircle className="mx-auto h-10 w-10 text-amber-600 dark:text-amber-400" />
        <p className="mt-4 text-zinc-600 dark:text-zinc-400">{error}</p>
      </div>
    );
  }

  const macroRegime = regime as MacroRegimeDto;

  const summary = summarizeRegime(macroRegime.indicators);
  const readings = Object.values(macroRegime.indicators);
  const indicatorCount = readings.length;
  const moverCount = readings.filter(
    (r) => signalSeverity(r.signal) >= MARKET_MOVER_THRESHOLD
  ).length;

  return (
    <div className="mx-auto max-w-7xl space-y-10 px-4 py-8">
      <div>
        <h1 className="text-2xl font-bold text-zinc-900 dark:text-white">Macro Environment</h1>
        <p className="mt-1 text-sm text-zinc-500">
          As of{" "}
          {new Date(macroRegime.asOf).toLocaleDateString("en-US", {
            year: "numeric",
            month: "long",
            day: "numeric",
          })}
        </p>
      </div>

      {/* Pro waitlist — first thing on the page, narrow and centered */}
      <div className="mx-auto max-w-2xl">
        <ProWaitlist source="dashboard" />
      </div>

      {/* Asset search — the primary action, front and center */}
      <section className="rounded-2xl border border-emerald-500/30 bg-white dark:bg-zinc-900 p-6">
        <h2 className="mb-1 text-lg font-semibold text-zinc-900 dark:text-white">Analyze an Asset</h2>
        <p className="mb-4 text-sm text-zinc-500">
          Enter any symbol to see historical price odds and macro context.
        </p>
        <AssetSearch />
      </section>

      {/* Popular assets — live sparklines + our 3M odds */}
      {popular.length > 0 && (
        <section>
          <h2 className="mb-3 text-lg font-semibold text-zinc-900 dark:text-white">
            Popular Assets
          </h2>
          <PopularAssets assets={popular} />
        </section>
      )}

      {/* Full indicator grid lives on its own page — one glowing door to it */}
      <section>
        <LiveMacroCta moverCount={moverCount} indicatorCount={indicatorCount} />
      </section>

      {/* Historical matches */}
      {matches.length > 0 && (
        <section>
          <h2 className="mb-3 flex items-center gap-2 text-lg font-semibold text-zinc-900 dark:text-white">
            Most Similar Historical Periods
            <InfoTip align="left">
              <span className="block">
                The closest historical analogs to today&apos;s macro conditions. Each chip
                represents its whole surrounding period — nearby months are collapsed so one era
                can&apos;t occupy several slots.
              </span>
              <span className="mt-1.5 block">
                Asset odds are computed from the top 40 such analogs, weighted by closeness.
                Hover a chip for what its numbers mean.
              </span>
            </InfoTip>
          </h2>
          <div className="flex flex-wrap gap-2">
            {matches.map((m) => {
              // Guard against cached/older API responses that lack topPercent
              const hasPct = typeof m.topPercent === "number" && !Number.isNaN(m.topPercent);
              const topPct = hasPct ? Math.max(1, Math.ceil(m.topPercent)) : null;

              return (
                <span
                  key={m.date}
                  title={
                    (hasPct
                      ? `This month is closer to today's macro conditions than ${(100 - m.topPercent).toFixed(0)}% of all months since 1990. `
                      : "") +
                    `Similarity ${m.similarityScore.toFixed(1)}/100 measures how closely that month's indicators match today's. Exact repeats never happen, so even the best match in 30+ years scores around 65 — anything above ~55 is unusually strong.`
                  }
                  className="cursor-help rounded-full border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 px-3 py-1 text-sm text-zinc-700 dark:text-zinc-300"
                >
                  {new Date(m.date).toLocaleDateString("en-US", {
                    year: "numeric",
                    month: "short",
                  })}{" "}
                  {topPct !== null && (
                    <span className="font-medium text-emerald-600 dark:text-emerald-400">
                      top {topPct}%
                    </span>
                  )}{" "}
                  <span className="text-xs text-zinc-500">
                    · similarity {m.similarityScore.toFixed(0)}/100
                  </span>
                </span>
              );
            })}
          </div>
        </section>
      )}

      {/* Regime summary — the picture in plain words */}
      <section className="rounded-2xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-6">
        <div className="mb-3 flex items-center gap-2">
          <ScrollText className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
          <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">
            The Picture Right Now
          </h2>
        </div>
        <div className="space-y-2">
          {summary.map((sentence, i) => (
            <p key={i} className="text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">
              {sentence}
            </p>
          ))}
        </div>
        <p className="mt-4 border-t border-zinc-200 dark:border-zinc-800 pt-3 text-xs text-zinc-500">
          Generated automatically from the indicator signals above. Not financial advice.
        </p>
      </section>
    </div>
  );
}
