"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { track } from "@/lib/analytics";
import { formatPrice } from "@/lib/format";
import type { AssetPeriodOddsDto } from "@/types";
import { Loader2, AlertCircle, Lock } from "lucide-react";

const PRESETS: { label: string; days: number }[] = [
  { label: "1W", days: 7 },
  { label: "2W", days: 14 },
  { label: "1M", days: 30 },
  { label: "2M", days: 60 },
  { label: "3M", days: 90 },
  { label: "6M", days: 180 },
  { label: "9M", days: 270 },
  { label: "1Y", days: 365 },
];

function periodLabel(days: number): string {
  const preset = PRESETS.find((p) => p.days === days);
  if (preset) return preset.label;
  return `${days} days`;
}

function Target({
  label,
  price,
  currentPrice,
  symbol,
}: {
  label: string;
  price: number | null;
  currentPrice: number | null;
  symbol: string;
}) {
  if (price === null || currentPrice === null) return null;
  const pct = ((price - currentPrice) / currentPrice) * 100;

  return (
    <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
      <div className="text-xs text-zinc-500">{label}</div>
      <div className="mt-1 text-lg font-bold text-zinc-900 dark:text-white tabular-nums">{formatPrice(price, symbol)}</div>
      <div className={`text-xs mt-0.5 ${pct >= 0 ? "text-emerald-600 dark:text-emerald-400" : "text-red-600 dark:text-red-400"}`}>
        {pct >= 0 ? "+" : ""}{pct.toFixed(1)}%
      </div>
    </div>
  );
}

// One keyed result instead of separate loading/gated/error flags: "loading" is
// derived from a key mismatch, so the effect never resets state synchronously.
type PredictorResult =
  | { key: string; kind: "data"; data: AssetPeriodOddsDto }
  | { key: string; kind: "gated" }
  | { key: string; kind: "error"; message: string };

export function PeriodPredictor({ symbol }: { symbol: string }) {
  const [days, setDays] = useState(30);
  const [result, setResult] = useState<PredictorResult | null>(null);
  const requestKey = `${symbol}:${days}`;

  useEffect(() => {
    let cancelled = false;

    // Debounce so dragging the slider doesn't fire a request per pixel
    const timer = setTimeout(() => {
      api
        .getOddsForPeriod(symbol, days)
        .then((r) => {
          if (!cancelled) setResult({ key: requestKey, kind: "data", data: r });
        })
        .catch((err) => {
          if (cancelled) return;
          if (err instanceof ApiError && err.status === 401) {
            setResult({ key: requestKey, kind: "gated" });
            track("gate_hit", { gate: "custom-window", symbol });
          } else {
            setResult({ key: requestKey, kind: "error", message: "Could not compute odds for this period." });
          }
        });
    }, 300);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [symbol, days, requestKey]);

  const loading = result?.key !== requestKey;
  // The gate is symbol-wide — once hit, keep showing it across window changes
  const gated = result?.kind === "gated";
  const error = !loading && result?.kind === "error" ? result.message : null;
  // Previous window's numbers stay visible (dimmed) while the next loads
  const data = result?.kind === "data" ? result.data : null;
  const odds = data?.odds;
  const hasData = odds && odds.totalCases > 0;

  return (
    <div className="rounded-xl border border-emerald-500/30 bg-white dark:bg-zinc-900 p-5">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">Custom Prediction Window</h2>
        {loading && <Loader2 className="h-4 w-4 animate-spin text-zinc-500" />}
      </div>

      <div className="mb-3 flex flex-wrap gap-2">
        {PRESETS.map((p) => (
          <button
            key={p.days}
            onClick={() => setDays(p.days)}
            className={`rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ${
              days === p.days
                ? "border-emerald-500 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                : "border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 hover:border-zinc-400 dark:hover:border-zinc-600 hover:text-zinc-900 dark:hover:text-white"
            }`}
          >
            {p.label}
          </button>
        ))}
      </div>

      <div className="mb-5 flex items-center gap-3">
        <input
          type="range"
          min={7}
          max={365}
          value={days}
          onChange={(e) => setDays(Number(e.target.value))}
          className="flex-1 accent-emerald-500"
        />
        <span className="w-20 text-right text-sm text-zinc-600 dark:text-zinc-400 tabular-nums">
          {days} days
        </span>
      </div>

      {gated ? (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-4">
          <Lock className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span className="text-sm text-zinc-700 dark:text-zinc-300">
            Custom prediction windows are a free-account feature.
          </span>
          <Link
            href={`/auth?returnTo=${encodeURIComponent(`/odds/${symbol}`)}`}
            className="rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-emerald-500"
          >
            Sign up free
          </Link>
        </div>
      ) : error ? (
        <div className="flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      ) : hasData ? (
        <div className={loading ? "opacity-50 transition-opacity" : "transition-opacity"}>
          <div className="mb-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <span
              className={`text-3xl font-bold ${
                odds.positiveOdds >= 65
                  ? "text-emerald-600 dark:text-emerald-400"
                  : odds.positiveOdds >= 50
                  ? "text-zinc-900 dark:text-zinc-100"
                  : "text-red-600 dark:text-red-400"
              }`}
            >
              {odds.positiveOdds.toFixed(0)}%
            </span>
            <span className="text-sm text-zinc-600 dark:text-zinc-400">
              chance {symbol} is higher in {periodLabel(days)}
            </span>
            {odds.baseRate !== null && (
              <span
                title={`${symbol} was higher after ${periodLabel(days)} in ${odds.baseRate.toFixed(0)}% of ALL historical periods — the base rate. The current macro regime ${Math.abs(odds.edge) <= 1 ? "does not meaningfully shift" : odds.edge > 0 ? "improves" : "worsens"} those odds.`}
                className={`cursor-help rounded-md border px-2 py-0.5 text-xs font-medium ${
                  odds.edge > 1
                    ? "border-emerald-400/30 bg-emerald-400/10 text-emerald-600 dark:text-emerald-400"
                    : odds.edge < -1
                    ? "border-red-400/30 bg-red-400/10 text-red-600 dark:text-red-400"
                    : "border-zinc-400 dark:border-zinc-600 bg-zinc-100 dark:bg-zinc-800 text-zinc-600 dark:text-zinc-400"
                }`}
              >
                {odds.edge >= 0 ? "+" : ""}{odds.edge.toFixed(1)}pp vs base rate
              </span>
            )}
          </div>
          <div className="mt-4 grid grid-cols-3 gap-3">
            <Target
              label="Conservative (P25)"
              price={odds.priceTargetLow}
              currentPrice={data.currentPrice}
              symbol={symbol}
            />
            <Target
              label="Base Case (P50)"
              price={odds.priceTargetMid}
              currentPrice={data.currentPrice}
              symbol={symbol}
            />
            <Target
              label="Optimistic (P75)"
              price={odds.priceTargetHigh}
              currentPrice={data.currentPrice}
              symbol={symbol}
            />
          </div>

          <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-xs text-zinc-500">
            <span>Avg: <span className={odds.averageReturn >= 0 ? "text-emerald-600 dark:text-emerald-400" : "text-red-600 dark:text-red-400"}>
              {odds.averageReturn >= 0 ? "+" : ""}{odds.averageReturn.toFixed(1)}%
            </span></span>
            <span>Best: <span className="text-emerald-600 dark:text-emerald-400">+{odds.bestCase.toFixed(1)}%</span></span>
            <span>Worst: <span className="text-red-600 dark:text-red-400">{odds.worstCase.toFixed(1)}%</span></span>
          </div>
        </div>
      ) : !loading ? (
        <p className="text-sm text-zinc-500">
          Not enough historical data to compute odds for this period.
        </p>
      ) : null}
    </div>
  );
}
