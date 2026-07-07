import type { AnalogBreakdownDto } from "@/types";
import { SplitSquareHorizontal } from "lucide-react";

// The analogs split by the asset's own price state at the time — the honest
// answer to "your dots appear before rises AND falls, what gives?". The split
// is shown as data with sample sizes; it deliberately does not change the
// headline odds because it failed our out-of-sample validation.
export function AnalogContext({ breakdown, symbol }: { breakdown: AnalogBreakdownDto; symbol: string }) {
  const known = breakdown.aboveCount + breakdown.belowCount;
  if (known < 6) return null; // too few state-known analogs to be worth showing

  const current = breakdown.currentAboveMa200;

  const groups = [
    {
      label: "trading above its 200-day average",
      dotClass: "bg-sky-500",
      count: breakdown.aboveCount,
      odds: breakdown.aboveOdds3M,
      median: breakdown.aboveMedian3M,
      matchesToday: current === true,
    },
    {
      label: "trading below its 200-day average",
      dotClass: "bg-amber-500",
      count: breakdown.belowCount,
      odds: breakdown.belowOdds3M,
      median: breakdown.belowMedian3M,
      matchesToday: current === false,
    },
  ];

  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-5">
      <h2 className="mb-1 flex items-center gap-2 text-sm font-semibold text-zinc-900 dark:text-white">
        <SplitSquareHorizontal className="h-4 w-4 text-zinc-500" />
        Same Macro, Different Price Situations
      </h2>
      <p className="mb-4 text-xs text-zinc-500">
        The macro analogs happened at very different points in {symbol}&apos;s own price cycle —
        that&apos;s why dots on the chart appear before both rises and falls. Note: &quot;above the
        200-day average&quot; is a slow long-term measure, not &quot;currently rising&quot; — a price
        mid-fall can stay above its lagging average for weeks. Here is how each group played out
        over the following 3 months:
      </p>

      <div className="space-y-2">
        {groups.map((g) => (
          <div
            key={g.label}
            className={`flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 ${
              g.matchesToday
                ? "border-emerald-500/50 bg-emerald-400/5"
                : "border-zinc-200 dark:border-zinc-800"
            }`}
          >
            <div className="flex items-center gap-2 text-sm">
              <span className={`inline-block h-2.5 w-2.5 rounded-full ${g.dotClass}`} />
              <span className="text-zinc-700 dark:text-zinc-300">
                {symbol} was {g.label}
              </span>
              {g.matchesToday && (
                <span className="rounded-md border border-emerald-500/40 bg-emerald-400/10 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-emerald-700 dark:text-emerald-400">
                  today&apos;s situation
                </span>
              )}
            </div>
            <div className="text-sm tabular-nums text-zinc-600 dark:text-zinc-400">
              {g.count} analogs
              {g.odds !== null && (
                <>
                  {" · "}
                  <span className="font-medium text-zinc-900 dark:text-zinc-200">
                    {g.odds.toFixed(0)}% rose
                  </span>
                </>
              )}
              {g.median !== null && (
                <>
                  {" · median "}
                  <span
                    className={
                      g.median >= 0
                        ? "font-medium text-emerald-600 dark:text-emerald-400"
                        : "font-medium text-red-600 dark:text-red-400"
                    }
                  >
                    {g.median >= 0 ? "+" : ""}
                    {g.median.toFixed(1)}%
                  </span>
                </>
              )}
            </div>
          </div>
        ))}
      </div>

      <p className="mt-3 text-xs leading-relaxed text-zinc-500">
        Honesty note: we tested using this split to sharpen the headline odds, and it did not
        survive out-of-sample validation — patterns like &quot;low price then rose&quot; described
        the past better than they predicted the future. So we show you the split as context and
        keep the headline odds based on all analogs. With {known} state-known analogs the group
        samples are small; read the medians as rough tendencies, not targets.
      </p>
    </div>
  );
}
