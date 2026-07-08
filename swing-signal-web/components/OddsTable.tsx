import type { AssetOddsDto, OddsForPeriodDto } from "@/types";
import { Info } from "lucide-react";

// Plain-language definitions shown as native tooltips (they survive the
// table's horizontal-scroll container, where hover balloons would clip)
const METRIC_INFO: Record<string, string> = {
  "Positive Odds":
    "The chance this asset ends higher over this window, based on what happened after similar past macro periods. Closer matches count more, and the number is pulled toward the Base Rate so a few analogs can't produce extreme claims.",
  "Base Rate (all periods)":
    "How often this asset rose over windows of this length across its ENTIRE history, ignoring macro conditions. This is the 'default' — what the odds would be on a completely average day.",
  "Regime Edge":
    "Positive Odds minus Base Rate: how much today's macro environment shifts the odds versus normal. Small numbers are honest — most of the time, macro doesn't change much.",
  "Avg Return":
    "The average return after each similar past period (closer matches weighted more). One huge outcome can drag it up or down — compare it with the median.",
  "Median Return":
    "The middle outcome: half of the similar past periods ended better than this, half worse. More robust than the average when one outcome was extreme.",
  "Best Case":
    "The single best outcome among the similar past periods. A lucky extreme that happened once — not a target.",
  "Worst Case":
    "The single worst outcome among the similar past periods. The realistic bad scenario — it actually happened once in comparable conditions.",
};

function MetricLabel({ label, emphasized = false }: { label: string; emphasized?: boolean }) {
  return (
    <span
      title={METRIC_INFO[label]}
      className={`inline-flex cursor-help items-center gap-1.5 ${
        emphasized ? "font-medium text-zinc-700 dark:text-zinc-300" : "text-zinc-600 dark:text-zinc-400"
      }`}
    >
      {label}
      <Info className="h-3 w-3 shrink-0 text-zinc-400 dark:text-zinc-500" />
    </span>
  );
}

function oddsColor(pct: number) {
  if (pct >= 65) return "text-emerald-600 dark:text-emerald-400 font-semibold";
  if (pct >= 50) return "text-zinc-800 dark:text-zinc-200";
  return "text-red-600 dark:text-red-400";
}

function returnColor(v: number) {
  if (v > 0) return "text-emerald-600 dark:text-emerald-400";
  if (v < 0) return "text-red-600 dark:text-red-400";
  return "text-zinc-600 dark:text-zinc-400";
}

function fmt(v: number, suffix = "%") {
  return `${v >= 0 ? "+" : ""}${v.toFixed(1)}${suffix}`;
}

function PeriodCell({ d }: { d: OddsForPeriodDto }) {
  return (
    <td className="px-4 py-3 text-center">
      <div className={`text-lg ${oddsColor(d.positiveOdds)}`}>
        {d.positiveOdds.toFixed(0)}%
      </div>
      <div className="mt-0.5 text-xs text-zinc-500">
        {d.positiveCases}/{d.totalCases} cases
      </div>
    </td>
  );
}

function ReturnCell({ d, field }: { d: OddsForPeriodDto; field: keyof OddsForPeriodDto }) {
  const v = d[field] as number;
  return (
    <td className={`px-4 py-3 text-center text-sm tabular-nums ${returnColor(v)}`}>
      {fmt(v)}
    </td>
  );
}

const PERIODS: { label: string; key: keyof AssetOddsDto }[] = [
  { label: "1 Month", key: "oneMonth" },
  { label: "3 Months", key: "threeMonths" },
  { label: "6 Months", key: "sixMonths" },
];

interface Props {
  odds: AssetOddsDto;
}

export function OddsTable({ odds }: Props) {
  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 overflow-x-auto">
      <table className="w-full text-sm text-zinc-700 dark:text-zinc-300">
        <thead>
          <tr className="border-b border-zinc-200 dark:border-zinc-800">
            <th className="px-4 py-3 text-left text-xs uppercase tracking-wider text-zinc-500">
              Metric
            </th>
            {PERIODS.map((p) => (
              <th
                key={p.key}
                className="px-4 py-3 text-center text-xs uppercase tracking-wider text-zinc-500"
              >
                {p.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-zinc-200 dark:divide-zinc-800">
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Positive Odds" emphasized /></td>
            {PERIODS.map((p) => (
              <PeriodCell key={p.key} d={odds[p.key] as OddsForPeriodDto} />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Base Rate (all periods)" /></td>
            {PERIODS.map((p) => {
              const d = odds[p.key] as OddsForPeriodDto;
              return (
                <td key={p.key} className="px-4 py-3 text-center text-sm text-zinc-600 dark:text-zinc-400 tabular-nums">
                  {d.baseRate !== null ? `${d.baseRate.toFixed(0)}%` : "—"}
                </td>
              );
            })}
          </tr>
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Regime Edge" /></td>
            {PERIODS.map((p) => {
              const d = odds[p.key] as OddsForPeriodDto;
              return (
                <td
                  key={p.key}
                  className={`px-4 py-3 text-center text-sm font-medium tabular-nums ${
                    d.edge > 1 ? "text-emerald-600 dark:text-emerald-400" : d.edge < -1 ? "text-red-600 dark:text-red-400" : "text-zinc-600 dark:text-zinc-400"
                  }`}
                >
                  {d.baseRate !== null ? `${d.edge >= 0 ? "+" : ""}${d.edge.toFixed(1)}pp` : "—"}
                </td>
              );
            })}
          </tr>
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Avg Return" /></td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="averageReturn" />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Median Return" /></td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="medianReturn" />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Best Case" /></td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="bestCase" />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3"><MetricLabel label="Worst Case" /></td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="worstCase" />
            ))}
          </tr>
        </tbody>
      </table>
    </div>
  );
}
