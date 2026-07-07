"use client";

import { useEffect, useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { track } from "@/lib/analytics";
import { useAuth } from "@/context/AuthContext";
import { OddsTable } from "@/components/OddsTable";
import { AssetSearch } from "@/components/AssetSearch";
import { PeriodPredictor } from "@/components/PeriodPredictor";
import { BacktestPanel } from "@/components/BacktestPanel";
import { PriceChart } from "@/components/PriceChart";
import { ProWaitlist } from "@/components/ProWaitlist";
import { AlertCircle, ArrowLeft, Info, Loader2, Lock } from "lucide-react";
import type { AssetOddsDto, OddsForPeriodDto } from "@/types";
import { formatPrice } from "@/lib/format";

function PriceTargetCard({
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
    <div className="rounded-xl border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-900 p-4">
      <div className="text-xs text-zinc-500 mb-1">{label}</div>
      <div className="text-xl font-bold text-zinc-900 dark:text-white">{formatPrice(price, symbol)}</div>
      <div className={`text-sm mt-0.5 ${pct >= 0 ? "text-emerald-600 dark:text-emerald-400" : "text-red-600 dark:text-red-400"}`}>
        {pct >= 0 ? "+" : ""}{pct.toFixed(1)}% from current
      </div>
    </div>
  );
}

function PeriodTargets({
  label,
  period,
  currentPrice,
  symbol,
}: {
  label: string;
  period: OddsForPeriodDto;
  currentPrice: number | null;
  symbol: string;
}) {
  if (period.totalCases === 0) return null;

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold text-zinc-700 dark:text-zinc-300">{label}</h3>
        <span
          className={`text-sm font-medium ${
            period.positiveOdds >= 65
              ? "text-emerald-600 dark:text-emerald-400"
              : period.positiveOdds >= 50
              ? "text-zinc-700 dark:text-zinc-300"
              : "text-red-600 dark:text-red-400"
          }`}
        >
          {period.positiveOdds.toFixed(0)}% chance of positive move
          {period.baseRate !== null && (
            <span className="ml-2 text-xs text-zinc-500">
              (base {period.baseRate.toFixed(0)}%, edge {period.edge >= 0 ? "+" : ""}
              {period.edge.toFixed(1)}pp)
            </span>
          )}
        </span>
      </div>
      <div className="grid grid-cols-3 gap-3">
        <PriceTargetCard label="Conservative (P25)" price={period.priceTargetLow} currentPrice={currentPrice} symbol={symbol} />
        <PriceTargetCard label="Base Case (P50)" price={period.priceTargetMid} currentPrice={currentPrice} symbol={symbol} />
        <PriceTargetCard label="Optimistic (P75)" price={period.priceTargetHigh} currentPrice={currentPrice} symbol={symbol} />
      </div>
    </div>
  );
}

function SignupGate({ symbol }: { symbol: string }) {
  return (
    <div className="rounded-xl border border-emerald-500/30 bg-white dark:bg-zinc-900 px-6 py-12 text-center">
      <Lock className="mx-auto h-8 w-8 text-emerald-600 dark:text-emerald-400" />
      <h2 className="mt-4 text-lg font-semibold text-zinc-900 dark:text-white">
        {symbol} analysis is an account feature
      </h2>
      <p className="mx-auto mt-2 max-w-md text-sm text-zinc-600 dark:text-zinc-400">
        BTC, SPY and Gold are free without an account. To analyze any other symbol —
        plus custom prediction windows — create a free account. Takes 20 seconds.
      </p>
      <Link
        href={`/auth?returnTo=${encodeURIComponent(`/odds/${symbol}`)}`}
        className="mt-5 inline-block rounded-lg bg-emerald-600 px-6 py-2.5 font-medium text-white transition-colors hover:bg-emerald-500"
      >
        Create free account
      </Link>
    </div>
  );
}

export default function OddsPage() {
  const params = useParams<{ symbol: string }>();
  const searchParams = useSearchParams();
  const symbol = decodeURIComponent(params.symbol);
  const { user, loading: authLoading } = useAuth();

  const [odds, setOdds] = useState<AssetOddsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [gated, setGated] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return; // wait until the token is loaded so the fetch carries it

    let cancelled = false;
    setLoading(true);
    setGated(false);
    setError(null);

    api
      .getAssetOdds(symbol, 10, {
        q: searchParams.get("q") ?? undefined,
        src: searchParams.get("src") ?? undefined,
      })
      .then((r) => {
        if (!cancelled) setOdds(r);
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiError && err.status === 401) {
          setGated(true);
          track("gate_hit", { gate: "symbol", symbol });
        } else {
          setError(`Could not load odds for "${symbol}". New symbols take a few seconds to fetch — try again shortly.`);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [symbol, authLoading, user?.email]);

  return (
    <div className="mx-auto max-w-5xl space-y-8 px-4 py-8">
      <Link
        href="/dashboard"
        className="inline-flex items-center gap-1.5 text-sm text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
      >
        <ArrowLeft className="h-3.5 w-3.5" />
        Back to Dashboard
      </Link>

      <section>
        <h2 className="mb-3 text-sm font-medium text-zinc-600 dark:text-zinc-400">Search another asset</h2>
        <AssetSearch />
      </section>

      {loading || authLoading ? (
        <div className="flex justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-zinc-500 dark:text-zinc-600" />
        </div>
      ) : gated ? (
        <SignupGate symbol={symbol} />
      ) : error ? (
        <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 px-6 py-12 text-center">
          <AlertCircle className="mx-auto h-8 w-8 text-amber-600 dark:text-amber-400" />
          <p className="mt-3 text-zinc-600 dark:text-zinc-400">{error}</p>
        </div>
      ) : odds ? (
        <>
          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-2xl font-bold text-zinc-900 dark:text-white">{odds.name || symbol}</h1>
              <p className="mt-0.5 text-sm text-zinc-500">{symbol}</p>
            </div>
            {odds.currentPrice !== null && (
              <div className="text-right">
                <div className="text-xs text-zinc-500">Current Price</div>
                <div className="text-2xl font-bold text-zinc-900 dark:text-white tabular-nums">
                  {odds.currentPrice !== null ? formatPrice(odds.currentPrice, symbol) : "—"}
                </div>
                <div className="text-xs text-zinc-500 mt-0.5">
                  Based on {odds.matchesUsed} historical macro periods
                </div>
              </div>
            )}
          </div>

          <PriceChart symbol={odds.symbol} />

          <PeriodPredictor symbol={odds.symbol} />

          <section className="space-y-6">
            <h2 className="text-lg font-semibold text-zinc-900 dark:text-white">Standard Outlooks</h2>
            <PeriodTargets label="1-Month Outlook" period={odds.oneMonth} currentPrice={odds.currentPrice} symbol={symbol} />
            <PeriodTargets label="3-Month Outlook" period={odds.threeMonths} currentPrice={odds.currentPrice} symbol={symbol} />
            <PeriodTargets label="6-Month Outlook" period={odds.sixMonths} currentPrice={odds.currentPrice} symbol={symbol} />
          </section>

          {odds.explanations.length > 0 && (
            <section>
              <h2 className="mb-4 text-lg font-semibold text-zinc-900 dark:text-white">Why — Macro Context</h2>
              <div className="space-y-3">
                {odds.explanations.map((bullet, i) => (
                  <div key={i} className="flex gap-3 rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-4">
                    <Info className="mt-0.5 h-4 w-4 shrink-0 text-zinc-500" />
                    <p className="text-sm leading-relaxed text-zinc-700 dark:text-zinc-300">{bullet}</p>
                  </div>
                ))}
              </div>
            </section>
          )}

          <section>
            <h2 className="mb-3 text-lg font-semibold text-zinc-900 dark:text-white">Detailed Statistics</h2>
            <OddsTable odds={odds} />
          </section>

          <BacktestPanel symbol={odds.symbol} />

          <ProWaitlist source="odds-page" />

          <p className="text-xs text-zinc-500 dark:text-zinc-600">{odds.disclaimer}</p>
        </>
      ) : null}
    </div>
  );
}
