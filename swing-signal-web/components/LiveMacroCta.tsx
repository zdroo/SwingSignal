import Link from "next/link";
import { ArrowRight, Gauge, Star } from "lucide-react";

// Gradient door to the /macro page, with the live market-mover count.
// Used on the dashboard and the landing page.
export function LiveMacroCta({
  moverCount,
  indicatorCount,
}: {
  moverCount: number;
  indicatorCount: number;
}) {
  return (
    <Link
      href="/macro"
      className="group flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-emerald-500/40 bg-linear-to-r from-emerald-500/10 via-teal-500/5 to-emerald-500/10 p-5 transition-all duration-200 hover:border-emerald-500/70 hover:shadow-lg hover:shadow-emerald-500/15"
    >
      <div className="flex items-center gap-4">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-emerald-500/15">
          <Gauge className="h-5 w-5 text-emerald-600 dark:text-emerald-400" />
        </span>
        <div>
          <div className="font-semibold text-zinc-900 dark:text-white">Live Macro Indicators</div>
          <div className="flex items-center gap-1.5 text-sm text-zinc-500">
            <Star className="h-3 w-3 fill-emerald-500 text-emerald-500" />
            {moverCount} of {indicatorCount} currently market-moving
          </div>
        </div>
      </div>
      <span className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white shadow-md shadow-emerald-600/30 transition-all group-hover:bg-emerald-500 group-hover:shadow-emerald-500/40">
        View all indicators
        <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
      </span>
    </Link>
  );
}
