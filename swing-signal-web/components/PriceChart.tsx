"use client";

import { useEffect, useRef, useState } from "react";
import {
  createChart,
  ColorType,
  type IChartApi,
  type ISeriesApi,
  type SeriesMarker,
  type Time,
} from "lightweight-charts";
import { api } from "@/lib/api";
import { useTheme } from "@/context/ThemeContext";
import type { AnalogPointDto } from "@/types";
import { LineChart, Loader2 } from "lucide-react";

// Price history with the historical macro analogs marked on it — the visual
// explanation of the whole methodology: "these are the moments history says
// look like today, and the odds come from what happened after each one."
// Dots are colored by the asset's own price state at the time (uptrend vs
// downtrend), which explains why analogs precede both rises and falls.
export function PriceChart({ symbol, analogs }: { symbol: string; analogs?: AnalogPointDto[] }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<"Area"> | null>(null);

  const { theme } = useTheme();
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [markerStats, setMarkerStats] = useState({ above: 0, below: 0, unknown: 0, offChart: 0 });

  // Create the chart + load data once per symbol
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    let cancelled = false;

    const chart = createChart(container, {
      height: 320,
      autoSize: true,
      layout: {
        background: { type: ColorType.Solid, color: "transparent" },
        textColor: "#71717a",
        attributionLogo: false,
      },
      grid: {
        vertLines: { color: "rgba(113, 113, 122, 0.12)" },
        horzLines: { color: "rgba(113, 113, 122, 0.12)" },
      },
      rightPriceScale: { borderVisible: false },
      timeScale: { borderVisible: false },
    });
    chartRef.current = chart;

    const series = chart.addAreaSeries({
      lineColor: "#10b981",
      topColor: "rgba(16, 185, 129, 0.25)",
      bottomColor: "rgba(16, 185, 129, 0.0)",
      lineWidth: 2,
      priceLineVisible: false,
    });
    seriesRef.current = series;

    const analogsPromise = analogs
      ? Promise.resolve(analogs)
      : api.getHistoricalMatches(40).then((ms) =>
          ms.map((m): AnalogPointDto => ({ date: m.date, aboveMa200: null })));

    Promise.all([api.getCandles(symbol), analogsPromise])
      .then(([candles, analogPoints]) => {
        if (cancelled || candles.length === 0) return;

        const points = candles.map((c) => ({
          time: c.openTime.slice(0, 10) as Time,
          value: c.close,
        }));
        series.setData(points);

        // Mark analog dates inside this asset's price history, colored by the
        // asset's own state at the time: sky = uptrend, amber = downtrend
        const first = candles[0].openTime.slice(0, 10);
        const last = candles[candles.length - 1].openTime.slice(0, 10);

        const inRange = analogPoints
          .map((a) => ({ date: a.date.slice(0, 10), above: a.aboveMa200 }))
          .filter((a) => a.date >= first && a.date <= last)
          .sort((x, y) => x.date.localeCompare(y.date));

        const markers: SeriesMarker<Time>[] = inRange.map((a) => ({
          time: a.date as Time,
          position: "belowBar" as const,
          color: a.above === true ? "#0ea5e9" : a.above === false ? "#f59e0b" : "#71717a",
          shape: "circle" as const,
          size: 1,
        }));

        series.setMarkers(markers);
        setMarkerStats({
          above: inRange.filter((a) => a.above === true).length,
          below: inRange.filter((a) => a.above === false).length,
          unknown: inRange.filter((a) => a.above === null).length,
          offChart: analogPoints.length - inRange.length,
        });
        chart.timeScale().fitContent();
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
      chart.remove();
      chartRef.current = null;
      seriesRef.current = null;
    };
    // analogs arrive together with the page's odds fetch; rebuilding on its
    // identity would recreate the chart for no visual change
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [symbol]);

  // Recolor on theme change without rebuilding the chart
  useEffect(() => {
    chartRef.current?.applyOptions({
      layout: {
        background: { type: ColorType.Solid, color: "transparent" },
        textColor: theme === "dark" ? "#71717a" : "#52525b",
      },
      grid: {
        vertLines: { color: theme === "dark" ? "rgba(113,113,122,0.12)" : "rgba(113,113,122,0.18)" },
        horzLines: { color: theme === "dark" ? "rgba(113,113,122,0.12)" : "rgba(113,113,122,0.18)" },
      },
    });
  }, [theme]);

  if (failed) return null; // the chart is enrichment — never block the page

  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-4">
      <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-zinc-900 dark:text-white">
          <LineChart className="h-4 w-4 text-zinc-500" />
          Price History
        </h2>
        {markerStats.above + markerStats.below + markerStats.unknown > 0 && (
          <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-zinc-500">
            {markerStats.above > 0 && (
              <span className="flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 rounded-full bg-sky-500" />
                analog, asset in uptrend ({markerStats.above})
              </span>
            )}
            {markerStats.below > 0 && (
              <span className="flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 rounded-full bg-amber-500" />
                analog, asset in downtrend ({markerStats.below})
              </span>
            )}
            {markerStats.unknown > 0 && (
              <span className="flex items-center gap-1.5">
                <span className="inline-block h-2 w-2 rounded-full bg-zinc-500" />
                trend unknown — asset too young at the time ({markerStats.unknown})
              </span>
            )}
          </p>
        )}
      </div>

      <div className="relative">
        <div ref={containerRef} className="h-80 w-full" />
        {loading && (
          <div className="absolute inset-0 flex items-center justify-center">
            <Loader2 className="h-5 w-5 animate-spin text-zinc-500" />
          </div>
        )}
      </div>

      {markerStats.above + markerStats.below + markerStats.unknown > 0 && (
        <p className="mt-2 text-xs text-zinc-500">
          Each dot marks a month whose macro environment most closely resembled today&apos;s.
          The odds on this page are computed from what {symbol} did after those moments.
          {markerStats.offChart > 0 && (
            <>
              {" "}
              {markerStats.offChart} more analog{markerStats.offChart === 1 ? "" : "s"} predate{" "}
              {symbol}&apos;s available price history and can&apos;t be shown here — for those,
              the odds fall back to the analogs with price data.
            </>
          )}
        </p>
      )}
    </div>
  );
}
