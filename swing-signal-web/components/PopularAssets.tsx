import Link from "next/link";
import type { PopularAssetDto } from "@/types";
import { formatPrice } from "@/lib/format";

// Hand-rolled SVG sparkline — no chart library needed for a 30-point line
function Sparkline({ points, up }: { points: number[]; up: boolean }) {
  if (points.length < 2) return null;

  const w = 120;
  const h = 36;
  const pad = 2;

  const min = Math.min(...points);
  const max = Math.max(...points);
  const range = max - min || 1;

  const coords = points
    .map((p, i) => {
      const x = pad + (i / (points.length - 1)) * (w - pad * 2);
      const y = h - pad - ((p - min) / range) * (h - pad * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");

  const color = up ? "rgb(16 185 129)" : "rgb(239 68 68)"; // emerald-500 / red-500

  return (
    <svg viewBox={`0 0 ${w} ${h}`} className="h-9 w-full" preserveAspectRatio="none">
      <polyline
        points={coords}
        fill="none"
        stroke={color}
        strokeWidth="1.5"
        strokeLinejoin="round"
        strokeLinecap="round"
      />
    </svg>
  );
}

export function PopularAssets({ assets }: { assets: PopularAssetDto[] }) {
  if (assets.length === 0) return null;

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
      {assets.map((a) => {
        const up = a.changePct >= 0;

        return (
          <Link
            key={a.symbol}
            href={`/odds/${encodeURIComponent(a.symbol)}`}
            className="group rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-3.5 transition-colors hover:border-emerald-500/50"
          >
            <div className="flex items-baseline justify-between gap-2">
              <span className="text-sm font-semibold text-zinc-900 dark:text-white">
                {a.name}
              </span>
              <span
                className={`text-xs font-medium tabular-nums ${
                  up ? "text-emerald-600 dark:text-emerald-400" : "text-red-600 dark:text-red-400"
                }`}
              >
                {up ? "+" : ""}
                {a.changePct.toFixed(1)}%
              </span>
            </div>

            <div className="mt-0.5 text-xs text-zinc-500 tabular-nums">
              {formatPrice(a.currentPrice, a.symbol)}
            </div>

            <div className="mt-2">
              <Sparkline points={a.spark} up={up} />
            </div>

            {a.odds3M !== null && (
              <div className="mt-2 border-t border-zinc-100 dark:border-zinc-800 pt-2 text-xs text-zinc-500">
                3M odds{" "}
                <span className="font-semibold text-zinc-700 dark:text-zinc-300">
                  {a.odds3M.toFixed(0)}%
                </span>
                {a.edge3M !== null && (
                  <span
                    className={
                      a.edge3M > 1
                        ? "ml-1 text-emerald-600 dark:text-emerald-400"
                        : a.edge3M < -1
                        ? "ml-1 text-red-600 dark:text-red-400"
                        : "ml-1 text-zinc-500"
                    }
                  >
                    ({a.edge3M >= 0 ? "+" : ""}
                    {a.edge3M.toFixed(1)}pp)
                  </span>
                )}
              </div>
            )}
          </Link>
        );
      })}
    </div>
  );
}
