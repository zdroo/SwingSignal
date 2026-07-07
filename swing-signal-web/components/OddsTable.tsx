import type { AssetOddsDto, OddsForPeriodDto } from "@/types";

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
            <td className="px-4 py-3 font-medium text-zinc-700 dark:text-zinc-300">Positive Odds</td>
            {PERIODS.map((p) => (
              <PeriodCell key={p.key} d={odds[p.key] as OddsForPeriodDto} />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">Base Rate (all periods)</td>
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
            <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">Regime Edge</td>
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
            <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">Avg Return</td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="averageReturn" />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">Median Return</td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="medianReturn" />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">Best Case</td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="bestCase" />
            ))}
          </tr>
          <tr>
            <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">Worst Case</td>
            {PERIODS.map((p) => (
              <ReturnCell key={p.key} d={odds[p.key] as OddsForPeriodDto} field="worstCase" />
            ))}
          </tr>
        </tbody>
      </table>
    </div>
  );
}
