"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import type { MacroIndicatorValueDto } from "@/types";
import { getIndicator } from "@/lib/indicators";
import { signalSeverity, signalTag, MARKET_MOVER_THRESHOLD } from "@/lib/regime-insight";
import { TrendingUp, TrendingDown, Minus, Star } from "lucide-react";

// Estimated tooltip footprint used to decide placement before it's visible
const TOOLTIP_HEIGHT = 290;
const TOOLTIP_WIDTH = 288; // w-72
const VIEWPORT_MARGIN = 8;
const NAVBAR_HEIGHT = 56;

const GREEN = "text-emerald-600 dark:text-emerald-400 bg-emerald-400/10 border-emerald-400/30";
const RED   = "text-red-600 dark:text-red-400 bg-red-400/10 border-red-400/30";
const AMBER = "text-amber-600 dark:text-amber-400 bg-amber-400/10 border-amber-400/30";
const GRAY  = "text-zinc-600 dark:text-zinc-400 bg-zinc-400/10 border-zinc-400/30";

const SIGNAL_COLORS: Record<string, string> = {
  // bullish / healthy
  Accommodative: GREEN,
  Healthy: GREEN,
  Low: GREEN,
  Normal: GRAY,
  Expanding: GREEN,
  "QE (Expanding)": GREEN,
  "Negative (Easy)": GREEN,
  "On Target": GREEN,
  Strong: GREEN,
  Calm: GREEN,
  "No Signal": GREEN,
  "Growth Signal": GREEN,
  Optimistic: GREEN,
  "Risk-on": GREEN,
  "Weak USD": GREEN,

  // bearish / stressed
  Restrictive: RED,
  Inverted: RED,
  Contracting: RED,
  "QT (Contracting)": RED,
  "Contraction Signal": RED,
  Stressed: RED,
  "Recession Signal": RED,
  Panic: RED,
  Pessimistic: RED,
  Falling: RED,
  "Risk-off": RED,
  "Strong USD": RED,
  "Extreme Greed": RED,

  // caution
  Elevated: AMBER,
  High: AMBER,
  Flat: AMBER,
  Warning: AMBER,
  "Above Target": AMBER,
  Slow: AMBER,
  Inflationary: AMBER,
  Complacent: AMBER,
  Greed: AMBER,
  Fear: AMBER,
  "Extreme Fear": GREEN, // contrarian: extreme fear has historically been a buy zone

  // neutral
  Neutral: GRAY,
  Stable: GRAY,
  Moderate: GRAY,
  Deflationary: GRAY,
};

interface Props {
  indicatorKey: string;
  data: MacroIndicatorValueDto;
}

export function MacroIndicatorCard({ indicatorKey, data }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [placeAbove, setPlaceAbove] = useState(true);
  const [xShift, setXShift] = useState(0);

  const info = getIndicator(indicatorKey);
  const colorClass = SIGNAL_COLORS[data.signal] ?? GRAY;
  const label = info?.name ?? indicatorKey;
  const unit = info?.unit ?? "";
  const isMover = signalSeverity(data.signal) >= MARKET_MOVER_THRESHOLD;
  const tag = signalTag(indicatorKey, data.signal);

  // Decide tooltip placement from the card's position in the viewport,
  // so the balloon never renders off-screen.
  const updatePlacement = () => {
    const rect = containerRef.current?.getBoundingClientRect();
    if (!rect) return;

    setPlaceAbove(rect.top - NAVBAR_HEIGHT >= TOOLTIP_HEIGHT);

    const center = rect.left + rect.width / 2;
    const half = TOOLTIP_WIDTH / 2;
    let shift = 0;
    if (center - half < VIEWPORT_MARGIN) {
      shift = VIEWPORT_MARGIN - (center - half);
    } else if (center + half > window.innerWidth - VIEWPORT_MARGIN) {
      shift = window.innerWidth - VIEWPORT_MARGIN - (center + half);
    }
    setXShift(shift);
  };

  const TrendIcon =
    data.trend === "Rising"
      ? TrendingUp
      : data.trend === "Falling"
      ? TrendingDown
      : Minus;

  const trendColor =
    data.trend === "Rising"
      ? "text-emerald-600 dark:text-emerald-400"
      : data.trend === "Falling"
      ? "text-red-600 dark:text-red-400"
      : "text-zinc-500";

  return (
    <div ref={containerRef} onMouseEnter={updatePlacement} className="group relative h-full">
      <div className="h-full cursor-help rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-4 flex flex-col gap-3">
        <div className="flex items-start justify-between gap-2">
          <span className="text-sm font-medium text-zinc-700 dark:text-zinc-300">{label}</span>
          <span className="flex shrink-0 items-center gap-1">
            {isMover && (
              <Star className="h-3.5 w-3.5 fill-emerald-500 text-emerald-500" />
            )}
            <TrendIcon className={`h-4 w-4 ${trendColor}`} />
          </span>
        </div>

        <div className="text-2xl font-bold text-zinc-900 dark:text-white tabular-nums">
          {typeof data.value === "number"
            ? data.value.toLocaleString("en-US", { maximumFractionDigits: 2 })
            : "—"}
          {unit && <span className="ml-1 text-sm font-normal text-zinc-500">{unit}</span>}
        </div>

        <div className="mt-auto">
          <span
            className={`inline-flex w-fit items-center rounded-md border px-2 py-0.5 text-xs font-medium ${colorClass}`}
          >
            {data.signal}
          </span>
          {tag && (
            <p className="mt-1.5 text-xs italic text-zinc-500">{tag}</p>
          )}
        </div>
      </div>

      {/* Hover tooltip — flips below the card near the top of the viewport,
          shifts horizontally at screen edges */}
      {info && (
        <div
          style={{ transform: `translateX(calc(-50% + ${xShift}px))` }}
          className={`pointer-events-none absolute left-1/2 z-30 w-72 rounded-xl border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-800 p-4 opacity-0 shadow-xl transition-opacity duration-150 group-hover:pointer-events-auto group-hover:opacity-100 ${
            placeAbove ? "bottom-full mb-2" : "top-full mt-2"
          }`}
        >
        <p className="text-sm font-semibold text-zinc-900 dark:text-white">{info.name}</p>
          <p className="mt-1 text-xs leading-relaxed text-zinc-600 dark:text-zinc-300">{info.short}</p>
          <div className="mt-2 space-y-1.5 text-xs leading-relaxed">
            <p>
              <span className="font-semibold text-emerald-600 dark:text-emerald-400">Low: </span>
              <span className="text-zinc-600 dark:text-zinc-400">{info.low}</span>
            </p>
            <p>
              <span className="font-semibold text-amber-600 dark:text-amber-400">High: </span>
              <span className="text-zinc-600 dark:text-zinc-400">{info.high}</span>
            </p>
          </div>
          <p className="mt-2 border-t border-zinc-200 dark:border-zinc-700 pt-2 text-xs text-zinc-500">
            {info.bands}
          </p>
          <Link
            href={`/indicators#${indicatorKey}`}
            className="mt-1.5 inline-block text-xs font-medium text-emerald-600 dark:text-emerald-400 hover:text-emerald-500 dark:hover:text-emerald-300"
          >
            Learn more →
          </Link>
        </div>
      )}
    </div>
  );
}
