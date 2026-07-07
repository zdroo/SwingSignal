import { api } from "@/lib/api";
import { MacroIndicatorCard } from "@/components/MacroIndicatorCard";
import { AssetSearch } from "@/components/AssetSearch";
import { PopularAssets } from "@/components/PopularAssets";
import { signalSeverity, summarizeRegime, MARKET_MOVER_THRESHOLD } from "@/lib/regime-insight";
import type { MacroRegimeDto } from "@/types";
import { AlertCircle, Star, ScrollText } from "lucide-react";

// Grouped: policy & liquidity → rates → economy → market stress → commodities & sentiment
const INDICATOR_ORDER = [
  "FedFundsRate",
  "RealYield10Y",
  "FedBalanceSheet",
  "M2MoneySupply",
  "ReverseRepo",
  "TreasuryYield10Y",
  "TreasuryYield2Y",
  "TreasuryYield3M",
  "YieldCurveSpread",
  "YieldSpread10Y3M",
  "CPI",
  "CorePCE",
  "GDP",
  "UnemploymentRate",
  "JoblessClaims",
  "SahmRule",
  "RetailSales",
  "HousingStarts",
  "ConsumerSentiment",
  "VIX",
  "HighYieldSpread",
  "DollarIndex",
  "GoldPrice",
  "OilWTI",
  "Copper",
  "CryptoFearGreed",
];

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

  // Most market-moving readings first; family order breaks ties (stable sort)
  const indicators = INDICATOR_ORDER
    .filter((k) => k in macroRegime.indicators)
    .sort(
      (a, b) =>
        signalSeverity(macroRegime.indicators[b].signal) -
        signalSeverity(macroRegime.indicators[a].signal)
    );

  const summary = summarizeRegime(macroRegime.indicators);
  const moverCount = indicators.filter(
    (k) => signalSeverity(macroRegime.indicators[k].signal) >= MARKET_MOVER_THRESHOLD
  ).length;

  return (
    <div className="mx-auto max-w-7xl space-y-10 px-4 py-8">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-zinc-900 dark:text-white">Macro Environment</h1>
        <p className="mt-1 text-sm text-zinc-500">
          As of{" "}
          {new Date(macroRegime.asOf).toLocaleDateString("en-US", {
            year: "numeric",
            month: "long",
            day: "numeric",
          })}
          {" "}— showing current macro conditions and their historical signal
        </p>
      </div>

      {/* Asset search — the primary action, front and center */}
      <section className="rounded-2xl border border-emerald-500/30 bg-white dark:bg-zinc-900 p-6">
        <h2 className="mb-1 text-lg font-semibold text-zinc-900 dark:text-white">Analyze an Asset</h2>
        <p className="mb-4 text-sm text-zinc-500">
          Enter any symbol to see historical price odds and macro context for the current regime.
          New symbols are fetched automatically.
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

      {/* Indicator grid — sorted by current market impact */}
      <section>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">Indicators</h2>
          <p className="flex items-center gap-1.5 text-xs text-zinc-500">
            <Star className="h-3 w-3 fill-emerald-500 text-emerald-500" />
            currently market-moving ({moverCount} of {indicators.length}) — sorted by impact
          </p>
        </div>
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
          {indicators.map((key) => (
            <MacroIndicatorCard
              key={key}
              indicatorKey={key}
              data={macroRegime.indicators[key]}
            />
          ))}
        </div>
      </section>

      {/* Historical matches */}
      {matches.length > 0 && (
        <section>
          <h2 className="mb-1 text-lg font-semibold text-zinc-900 dark:text-white">Most Similar Historical Periods</h2>
          <p className="mb-3 text-sm text-zinc-500">
            The closest historical analogs to today&apos;s macro conditions. Each chip represents
            its whole surrounding period — nearby months are collapsed so one era can&apos;t occupy
            several slots. Asset odds are computed from the top 40 such analogs, weighted by closeness.
            Hover a chip for what the numbers mean.
          </p>
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
          Generated automatically from the indicator signals above — the same rules every
          time, no opinions. Historical context only, not financial advice.
        </p>
      </section>
    </div>
  );
}
