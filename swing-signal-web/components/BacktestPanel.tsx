"use client";

import { useState } from "react";
import Link from "next/link";
import { api, ApiError } from "@/lib/api";
import { track } from "@/lib/analytics";
import type { BacktestComparisonDto, BacktestResultDto } from "@/types";
import { FlaskConical, Loader2, AlertCircle, GitCompareArrows, Lock } from "lucide-react";
import { InfoTip } from "@/components/InfoTip";

const HORIZONS = [
  { label: "1M", days: 30 },
  { label: "3M", days: 90 },
  { label: "6M", days: 180 },
];

function gapColor(predicted: number, actual: number): string {
  const gap = Math.abs(predicted - actual);
  if (gap < 10) return "text-emerald-600 dark:text-emerald-400";
  if (gap < 20) return "text-amber-600 dark:text-amber-400";
  return "text-red-600 dark:text-red-400";
}

export function BacktestPanel({ symbol }: { symbol: string }) {
  const [days, setDays] = useState(90);
  const [result, setResult] = useState<BacktestResultDto | null>(null);
  const [comparison, setComparison] = useState<BacktestComparisonDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [comparing, setComparing] = useState(false);
  const [gated, setGated] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async (selectedDays: number) => {
    setDays(selectedDays);
    setLoading(true);
    setError(null);
    setGated(false);
    setComparison(null);
    try {
      const r = await api.runBacktest(symbol, selectedDays);
      setResult(r);
      track("backtest_run", { symbol, days: selectedDays });
    } catch (err) {
      if (err instanceof ApiError && err.status === 401) {
        setGated(true);
        track("gate_hit", { gate: "backtest", symbol });
      } else {
        setError("Backtest failed — the API may still be ingesting data for this asset.");
      }
    } finally {
      setLoading(false);
    }
  };

  const compare = async () => {
    setComparing(true);
    setError(null);
    try {
      const c = await api.compareBacktest(symbol, days);
      setComparison(c);
    } catch {
      setError("Comparison failed — the API may still be ingesting data for this asset.");
    } finally {
      setComparing(false);
    }
  };

  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-5">
      <div className="mb-4 flex items-center gap-2">
        <FlaskConical className="h-4 w-4 text-zinc-500" />
        <h2 className="flex items-center gap-2 text-lg font-semibold text-zinc-900 dark:text-white">
          Model Accuracy (Backtest)
          <InfoTip align="left">
            Walk-forward test: for every month in history, we reproduce what the model would
            have predicted at that time (using only data available then) and compare against
            what actually happened.
          </InfoTip>
        </h2>
      </div>

      <div className="mb-4 flex gap-2">
        {HORIZONS.map((h) => (
          <button
            key={h.days}
            onClick={() => run(h.days)}
            disabled={loading}
            className={`rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors disabled:opacity-50 ${
              result && days === h.days
                ? "border-emerald-500 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                : "border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 hover:border-zinc-400 dark:hover:border-zinc-600 hover:text-zinc-900 dark:hover:text-white"
            }`}
          >
            {loading && days === h.days ? (
              <Loader2 className="inline h-3.5 w-3.5 animate-spin" />
            ) : null}{" "}
            Test {h.label} predictions
          </button>
        ))}
      </div>

      {gated && (
        <div className="flex flex-wrap items-center gap-3 rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-4">
          <Lock className="h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span className="text-sm text-zinc-700 dark:text-zinc-300">
            Backtests require an account — create one free and verify our accuracy yourself.
          </span>
          <Link
            href={`/auth?returnTo=${encodeURIComponent(`/odds/${symbol}`)}`}
            className="rounded-lg bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-emerald-500"
          >
            Sign up free
          </Link>
        </div>
      )}

      {error && (
        <div className="flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400">
          <AlertCircle className="h-4 w-4" />
          {error}
        </div>
      )}

      {result && !loading && (
        <div className="space-y-4">
          {/* Headline stats */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="text-xs text-zinc-500">Predictions Tested</div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">{result.totalPredictions}</div>
              <div className="text-xs text-zinc-500 dark:text-zinc-600">
                {new Date(result.firstPrediction).getFullYear()}–
                {new Date(result.lastPrediction).getFullYear()}
              </div>
            </div>
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="text-xs text-zinc-500">Directional Accuracy</div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">
                {result.directionalAccuracy.toFixed(0)}%
              </div>
            </div>
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="text-xs text-zinc-500">Brier Score</div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">{result.brierScore.toFixed(3)}</div>
              <div className="text-xs text-zinc-500 dark:text-zinc-600">0.25 = coin flip</div>
            </div>
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="text-xs text-zinc-500">Predicted vs Actual</div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">
                {result.avgPredictedOdds.toFixed(0)}% / {result.actualPositiveRate.toFixed(0)}%
              </div>
            </div>
          </div>

          {/* Calibration table */}
          {result.calibration.length > 0 && (
            <div className="overflow-x-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-zinc-200 dark:border-zinc-800 text-xs uppercase tracking-wider text-zinc-500">
                    <th className="px-4 py-2 text-left">When Model Said</th>
                    <th className="px-4 py-2 text-center">Cases</th>
                    <th className="px-4 py-2 text-center">Avg Predicted</th>
                    <th className="px-4 py-2 text-center">Actually Went Up</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                  {result.calibration.map((b) => (
                    <tr key={b.predictedRange}>
                      <td className="px-4 py-2 font-medium text-zinc-700 dark:text-zinc-300">{b.predictedRange}</td>
                      <td className="px-4 py-2 text-center text-zinc-600 dark:text-zinc-400">{b.predictions}</td>
                      <td className="px-4 py-2 text-center text-zinc-600 dark:text-zinc-400">
                        {b.avgPredictedOdds.toFixed(0)}%
                      </td>
                      <td
                        className={`px-4 py-2 text-center font-semibold ${gapColor(
                          b.avgPredictedOdds,
                          b.actualPositiveRate
                        )}`}
                      >
                        {b.actualPositiveRate.toFixed(0)}%
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          <p className="text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">{result.interpretation}</p>

          {/* Algorithm comparison */}
          <button
            onClick={compare}
            disabled={comparing}
            className="flex items-center gap-2 rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 px-3 py-1.5 text-sm font-medium text-zinc-700 dark:text-zinc-300 transition-colors hover:border-zinc-400 dark:hover:border-zinc-600 hover:text-zinc-900 dark:hover:text-white disabled:opacity-50"
          >
            {comparing ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <GitCompareArrows className="h-3.5 w-3.5" />
            )}
            Compare vs naive baseline
          </button>

          {comparison && !comparing && (
            <div className="space-y-3 rounded-lg border border-zinc-200 dark:border-zinc-800 bg-zinc-100/60 dark:bg-zinc-950/50 p-4">
              <div className="grid grid-cols-3 gap-3 text-sm">
                <div />
                <div className="text-center text-xs uppercase tracking-wider text-zinc-500">
                  Baseline
                </div>
                <div className="text-center text-xs uppercase tracking-wider text-emerald-500">
                  Current
                </div>

                <div className="text-zinc-600 dark:text-zinc-400">Brier Score</div>
                <div className="text-center text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {comparison.baseline.brierScore.toFixed(3)}
                </div>
                <div
                  className={`text-center font-semibold tabular-nums ${
                    comparison.current.brierScore <= comparison.baseline.brierScore
                      ? "text-emerald-600 dark:text-emerald-400"
                      : "text-red-600 dark:text-red-400"
                  }`}
                >
                  {comparison.current.brierScore.toFixed(3)}
                </div>

                <div className="text-zinc-600 dark:text-zinc-400">Directional Accuracy</div>
                <div className="text-center text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {comparison.baseline.directionalAccuracy.toFixed(0)}%
                </div>
                <div
                  className={`text-center font-semibold tabular-nums ${
                    comparison.current.directionalAccuracy >= comparison.baseline.directionalAccuracy
                      ? "text-emerald-600 dark:text-emerald-400"
                      : "text-red-600 dark:text-red-400"
                  }`}
                >
                  {comparison.current.directionalAccuracy.toFixed(0)}%
                </div>

                <div className="text-zinc-600 dark:text-zinc-400">Predictions</div>
                <div className="text-center text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {comparison.baseline.totalPredictions}
                </div>
                <div className="text-center text-zinc-700 dark:text-zinc-300 tabular-nums">
                  {comparison.current.totalPredictions}
                </div>
              </div>
              <p className="text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">{comparison.summary}</p>
            </div>
          )}

          <p className="text-xs text-zinc-500 dark:text-zinc-600">{result.disclaimer}</p>
        </div>
      )}
    </div>
  );
}
