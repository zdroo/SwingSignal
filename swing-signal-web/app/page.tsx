import type { Metadata } from "next";
import Link from "next/link";
import { api } from "@/lib/api";

export const metadata: Metadata = {
  alternates: { canonical: "/" },
};
import { MacroIndicatorCard } from "@/components/MacroIndicatorCard";
import { PopularAssets } from "@/components/PopularAssets";
import { ProWaitlist } from "@/components/ProWaitlist";
import type { AssetOddsDto, MacroRegimeDto } from "@/types";
import {
  ArrowRight,
  Database,
  SearchCheck,
  LineChart,
  FlaskConical,
  ShieldCheck,
} from "lucide-react";

// The six most recognizable indicators for the live teaser strip
const TEASER_INDICATORS = [
  "FedFundsRate",
  "YieldCurveSpread",
  "CPI",
  "UnemploymentRate",
  "VIX",
  "CryptoFearGreed",
];

async function getLandingData() {
  try {
    const [regime, spyOdds, popular] = await Promise.all([
      api.getCurrentRegime(),
      api.getAssetOdds("SPY"),
      api.getPopularAssets().catch(() => []),
    ]);
    return { regime, spyOdds, popular };
  } catch {
    return { regime: null, spyOdds: null, popular: [] };
  }
}

function ExampleOddsCard({ odds }: { odds: AssetOddsDto }) {
  const tm = odds.threeMonths;
  if (tm.totalCases === 0) return null;

  return (
    <div className="rounded-2xl border border-emerald-500/30 bg-white dark:bg-zinc-900 p-6 shadow-xl shadow-emerald-500/5">
      <div className="mb-1 flex items-baseline justify-between">
        <span className="font-semibold text-zinc-900 dark:text-white">{odds.name}</span>
        <span className="text-xs text-zinc-500">next 3 months</span>
      </div>

      <div className="my-3 flex items-baseline gap-3">
        <span className="text-4xl font-bold text-emerald-600 dark:text-emerald-400">
          {tm.positiveOdds.toFixed(0)}%
        </span>
        <span className="text-sm text-zinc-600 dark:text-zinc-400">chance of a positive move</span>
      </div>

      {tm.baseRate !== null && (
        <p className="text-sm text-zinc-500">
          Base rate across all history: {tm.baseRate.toFixed(0)}%. Current macro
          regime shifts it by{" "}
          <span className={tm.edge >= 0 ? "text-emerald-600 dark:text-emerald-400" : "text-red-600 dark:text-red-400"}>
            {tm.edge >= 0 ? "+" : ""}
            {tm.edge.toFixed(1)}pp
          </span>
          .
        </p>
      )}

      <p className="mt-3 border-t border-zinc-200 dark:border-zinc-800 pt-3 text-xs text-zinc-500 dark:text-zinc-600">
        Based on {odds.matchesUsed} historical periods with macro conditions similar
        to today&apos;s. Live data — this is the actual product.
      </p>
    </div>
  );
}

export default async function LandingPage() {
  const { regime, spyOdds, popular } = await getLandingData();
  const macroRegime = regime as MacroRegimeDto | null;

  const teaserCards = macroRegime
    ? TEASER_INDICATORS.filter((k) => k in macroRegime.indicators)
    : [];

  return (
    <div className="mx-auto max-w-6xl px-4">
      {/* ============ HERO ============ */}
      <section className="py-16 text-center sm:py-24">
        <h1 className="mx-auto max-w-3xl text-4xl font-bold leading-tight text-zinc-900 dark:text-white sm:text-5xl">
          What are the <span className="text-emerald-600 dark:text-emerald-400">odds</span>?
        </h1>
        <p className="mx-auto mt-5 max-w-2xl text-lg leading-relaxed text-zinc-600 dark:text-zinc-400">
          See how assets historically performed in macro conditions like
          today&apos;s — with the receipts. No signals, no promises. Historical
          odds, honestly calibrated.
        </p>
        <div className="mt-8 flex justify-center gap-3">
          <Link
            href="/dashboard"
            className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-6 py-3 font-medium text-white transition-colors hover:bg-emerald-500"
          >
            See today&apos;s regime
            <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
        <p className="mt-3 text-xs text-zinc-500 dark:text-zinc-600">Free. No account needed.</p>

        {/* Pro waitlist — capture interest before visitors dive in */}
        <div className="mx-auto mt-10 max-w-2xl text-left">
          <ProWaitlist source="landing" />
        </div>
      </section>

      {/* ============ LIVE INDICATOR STRIP ============ */}
      {teaserCards.length > 0 && (
        <section className="pb-16">
          <p className="mb-4 text-center text-xs uppercase tracking-widest text-zinc-500 dark:text-zinc-600">
            Live macro conditions — right now
          </p>
          <div className="flex flex-wrap justify-center gap-3">
            {teaserCards.map((key) => (
              <div key={key} className="w-[calc(50%-0.375rem)] sm:w-[calc(33.333%-0.5rem)] lg:w-[calc(16.666%-0.625rem)]">
                <MacroIndicatorCard
                  indicatorKey={key}
                  data={macroRegime!.indicators[key]}
                />
              </div>
            ))}
          </div>
        </section>
      )}

      {/* ============ POPULAR ASSETS ============ */}
      {popular.length > 0 && (
        <section className="pb-20">
          <p className="mb-4 text-center text-xs uppercase tracking-widest text-zinc-500 dark:text-zinc-600">
            Popular assets — price and our 3-month odds
          </p>
          <PopularAssets assets={popular} />
        </section>
      )}

      {/* ============ EXAMPLE CARD ============ */}
      {spyOdds && spyOdds.currentPrice !== null && (
        <section className="pb-20">
          <div className="mx-auto grid max-w-4xl items-center gap-8 md:grid-cols-2">
            <div>
              <h2 className="text-2xl font-bold text-zinc-900 dark:text-white">
                The whole product in one card
              </h2>
              <p className="mt-3 leading-relaxed text-zinc-600 dark:text-zinc-400">
                Type any symbol — stocks, crypto, forex, commodities. We find the
                historical months where the macro environment looked most like
                today, and show you what actually happened next. The odds, the
                price ranges, and the reasoning.
              </p>
              <Link
                href="/odds/SPY"
                className="mt-4 inline-flex items-center gap-1.5 text-sm font-medium text-emerald-600 dark:text-emerald-400 hover:text-emerald-500 dark:hover:text-emerald-300"
              >
                Explore the full SPY breakdown
                <ArrowRight className="h-3.5 w-3.5" />
              </Link>
            </div>
            <ExampleOddsCard odds={spyOdds} />
          </div>
        </section>
      )}

      {/* ============ HOW IT WORKS ============ */}
      <section className="pb-20">
        <h2 className="mb-10 text-center text-2xl font-bold text-zinc-900 dark:text-white">
          How it works
        </h2>
        <div className="grid gap-6 md:grid-cols-3">
          {[
            {
              icon: Database,
              title: "26 macro indicators, back to 1990",
              text: "Fed policy, yield curve, inflation, jobs, liquidity, credit stress, VIX, commodities, crypto sentiment — refreshed automatically from official sources.",
            },
            {
              icon: SearchCheck,
              title: "Find the months that look like today",
              text: "Every month in history becomes a fingerprint of macro conditions. We measure which past periods most resemble the present — including whether the Fed was hiking or cutting.",
            },
            {
              icon: LineChart,
              title: "Show what happened next",
              text: "For your asset and your time window: the odds of a positive move, conservative-to-optimistic price ranges, and how that compares to the asset's normal behavior.",
            },
          ].map((step) => (
            <div
              key={step.title}
              className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-6"
            >
              <step.icon className="mb-4 h-6 w-6 text-emerald-600 dark:text-emerald-400" />
              <h3 className="mb-2 font-semibold text-zinc-900 dark:text-white">{step.title}</h3>
              <p className="text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">{step.text}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ============ HONESTY / BACKTEST ============ */}
      <section className="pb-20">
        <div className="rounded-2xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-8 sm:p-10">
          <div className="mx-auto max-w-3xl text-center">
            <FlaskConical className="mx-auto mb-4 h-8 w-8 text-emerald-600 dark:text-emerald-400" />
            <h2 className="text-2xl font-bold text-zinc-900 dark:text-white">
              Every prediction tool claims accuracy.
              <br className="hidden sm:block" /> We let you test ours.
            </h2>
            <p className="mt-4 leading-relaxed text-zinc-600 dark:text-zinc-400">
              Every asset page has a built-in backtest: we replay history month by
              month, reproduce what the model would have predicted at the time —
              using only data available then — and show you how those predictions
              compared to reality. When our model has no edge for an asset, the
              backtest says so. Openly.
            </p>
            <p className="mt-4 text-sm text-zinc-500">
              When we say 70%, you can check how often 70% turned out to be right.
              No other tool in this space shows you that.
            </p>
          </div>
        </div>
      </section>

      {/* ============ WHAT WE'RE NOT ============ */}
      <section className="pb-20">
        <div className="mx-auto max-w-3xl text-center">
          <ShieldCheck className="mx-auto mb-4 h-7 w-7 text-zinc-500" />
          <h2 className="text-xl font-bold text-zinc-900 dark:text-white">What we&apos;re not</h2>
          <p className="mt-3 leading-relaxed text-zinc-600 dark:text-zinc-400">
            No buy/sell signals. No &quot;guru&quot; calls. No 10x promises. Markets
            are mostly unpredictable, and anyone claiming otherwise is selling
            something. What we offer is context: how similar situations played out
            before, stated as honest probabilities you can verify.
          </p>
        </div>
      </section>

      {/* ============ FINAL CTA ============ */}
      <section className="pb-24 text-center">
        <Link
          href="/dashboard"
          className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-8 py-3.5 font-medium text-white transition-colors hover:bg-emerald-500"
        >
          Open the dashboard
          <ArrowRight className="h-4 w-4" />
        </Link>
      </section>
    </div>
  );
}
