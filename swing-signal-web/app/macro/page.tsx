import type { Metadata } from "next";
import Link from "next/link";
import { api } from "@/lib/api";
import { MacroIndicatorCard } from "@/components/MacroIndicatorCard";
import { signalSeverity, MARKET_MOVER_THRESHOLD } from "@/lib/regime-insight";
import type { MacroRegimeDto } from "@/types";
import { AlertCircle, ArrowLeft, BookOpen, Star } from "lucide-react";

export const metadata: Metadata = {
  title: "Live Macro Indicators",
  description:
    "All 26 macro indicators live — Fed policy, yield curve, inflation, credit stress and more — sorted by how market-moving each reading is right now.",
  alternates: { canonical: "/macro" },
};

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

export default async function MacroPage() {
  let regime: MacroRegimeDto | null = null;
  try {
    regime = await api.getCurrentRegime();
  } catch {
    regime = null;
  }

  if (!regime) {
    return (
      <div className="mx-auto max-w-7xl px-4 py-16 text-center">
        <AlertCircle className="mx-auto h-10 w-10 text-amber-600 dark:text-amber-400" />
        <p className="mt-4 text-zinc-600 dark:text-zinc-400">
          Could not reach the SwingSignal API. Make sure the backend is running.
        </p>
      </div>
    );
  }

  // Most market-moving readings first; family order breaks ties (stable sort)
  const indicators = INDICATOR_ORDER
    .filter((k) => k in regime.indicators)
    .sort(
      (a, b) =>
        signalSeverity(regime.indicators[b].signal) -
        signalSeverity(regime.indicators[a].signal)
    );

  const moverCount = indicators.filter(
    (k) => signalSeverity(regime.indicators[k].signal) >= MARKET_MOVER_THRESHOLD
  ).length;

  return (
    <div className="mx-auto max-w-7xl space-y-8 px-4 py-8">
      <Link
        href="/dashboard"
        className="inline-flex items-center gap-1.5 text-sm text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
      >
        <ArrowLeft className="h-3.5 w-3.5" />
        Back to Dashboard
      </Link>

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold text-zinc-900 dark:text-white">Live Macro Indicators</h1>
          <p className="mt-1 text-sm text-zinc-500">
            As of{" "}
            {new Date(regime.asOf).toLocaleDateString("en-US", {
              year: "numeric",
              month: "long",
              day: "numeric",
            })}
          </p>
        </div>
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
            data={regime.indicators[key]}
          />
        ))}
      </div>

      <Link
        href="/indicators"
        className="inline-flex items-center gap-2 text-sm text-emerald-600 dark:text-emerald-400 hover:underline"
      >
        <BookOpen className="h-4 w-4" />
        New to these terms? Read the indicator guide
      </Link>
    </div>
  );
}
