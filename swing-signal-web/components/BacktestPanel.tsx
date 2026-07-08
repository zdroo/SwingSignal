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
            <span className="block">
              Walk-forward test: for every month in history, we reproduce what the model would
              have predicted at that time (using only data available then) and compare against
              what actually happened.
            </span>
            <span className="mt-1.5 block">
              Each prediction is a probability — the chance {symbol} would be{" "}
              <span className="font-medium text-zinc-900 dark:text-white">higher after the
              chosen horizon</span> (1, 3 or 6 months). Not a price target, just up-or-not odds.
            </span>
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
              <div className="flex items-center gap-1.5 text-xs text-zinc-500">
                Predictions Tested
                <InfoTip align="left">
                  How many past months we tested. For each one, the model predicted — using only
                  data available at that time — the chance {symbol} would be higher after the
                  chosen horizon, and we then checked what the price actually did.
                </InfoTip>
              </div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">{result.totalPredictions}</div>
              <div className="text-xs text-zinc-500 dark:text-zinc-600">
                {new Date(result.firstPrediction).getFullYear()}–
                {new Date(result.lastPrediction).getFullYear()}
              </div>
            </div>
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="flex items-center gap-1.5 text-xs text-zinc-500">
                Directional Accuracy
                <InfoTip>
                  How often the direction alone was right: a prediction counts as &quot;up&quot;
                  when the model&apos;s odds were above 50%. Caution — for assets that rise most
                  of the time, always saying &quot;up&quot; also scores high, so use the Brier
                  score as the stricter measure.
                </InfoTip>
              </div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">
                {result.directionalAccuracy.toFixed(0)}%
              </div>
            </div>
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="flex items-center gap-1.5 text-xs text-zinc-500">
                Brier Score
                <InfoTip>
                  Grades the probabilities themselves, not just the direction: the average squared
                  gap between the predicted odds (60% = 0.6) and what happened (1 if it rose,
                  0 if not). Lower is better. Always guessing 50/50 scores 0.250; a perfect
                  oracle scores 0. Below 0.25 means the odds carried real information.
                </InfoTip>
              </div>
              <div className="mt-1 text-xl font-bold text-zinc-900 dark:text-white">{result.brierScore.toFixed(3)}</div>
              <div className="text-xs text-zinc-500 dark:text-zinc-600">0.25 = coin flip</div>
            </div>
            <div className="rounded-lg border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800/50 p-3">
              <div className="flex items-center gap-1.5 text-xs text-zinc-500">
                Predicted vs Actual
                <InfoTip align="right">
                  The average odds the model predicted across all tests vs how often {symbol}{" "}
                  actually rose. Close numbers mean the model is honest overall — it doesn&apos;t
                  systematically over-promise or under-promise.
                </InfoTip>
              </div>
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
                  {/* Native titles here — hover balloons would clip inside the scroll container */}
                  <tr className="border-b border-zinc-200 dark:border-zinc-800 text-xs uppercase tracking-wider text-zinc-500">
                    <th
                      className="cursor-help px-4 py-2 text-left"
                      title="All test predictions grouped by the odds the model gave. Each row asks: when the model said e.g. 60-70%, did the asset actually rise about that often? Rows where the last two columns are close mean the odds can be taken at face value."
                    >
                      When Model Said
                    </th>
                    <th
                      className="cursor-help px-4 py-2 text-center"
                      title="How many test predictions fell into this odds range. Small counts (under ~20) can be off just by chance."
                    >
                      Cases
                    </th>
                    <th
                      className="cursor-help px-4 py-2 text-center"
                      title="The average odds the model gave within this range."
                    >
                      Avg Predicted
                    </th>
                    <th
                      className="cursor-help px-4 py-2 text-center"
                      title="How often the asset actually ended higher in those cases. Green = within 10 points of predicted, amber = within 20, red = further off."
                    >
                      Actually Went Up
                    </th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
                  {result.calibration.map((b) => (
                    <tr
                      key={b.predictedRange}
                      className="cursor-help"
                      title={`The model gave odds in the ${b.predictedRange} range ${b.predictions} time${b.predictions === 1 ? "" : "s"} (average ${b.avgPredictedOdds.toFixed(0)}%). ${symbol} actually rose in ${b.actualPositiveRate.toFixed(0)}% of those cases — a gap of ${Math.abs(b.avgPredictedOdds - b.actualPositiveRate).toFixed(0)} points. The closer the two numbers, the more these odds can be trusted at face value.`}
                    >
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
                <div
                  className="cursor-help text-center text-xs uppercase tracking-wider text-zinc-500"
                  title="A naive predictor that always guesses the asset's long-run base rate and ignores macro entirely. Our model has to beat this to be adding any value."
                >
                  Baseline
                </div>
                <div
                  className="cursor-help text-center text-xs uppercase tracking-wider text-emerald-500"
                  title="The live model: odds from the most similar historical macro periods."
                >
                  Current
                </div>

                <div
                  className="cursor-help text-zinc-600 dark:text-zinc-400"
                  title="Grades the probabilities: average squared gap between predicted odds and what happened. Lower is better; 0.250 = always guessing 50/50."
                >
                  Brier Score
                </div>
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

                <div
                  className="cursor-help text-zinc-600 dark:text-zinc-400"
                  title="How often the up-or-down call was right (odds above 50% count as 'up'). For assets that usually rise, this flatters both models — Brier is the stricter test."
                >
                  Directional Accuracy
                </div>
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
