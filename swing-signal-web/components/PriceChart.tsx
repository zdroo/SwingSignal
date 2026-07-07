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
import { LineChart, Loader2 } from "lucide-react";

// Price history with the historical macro analogs marked on it — the visual
// explanation of the whole methodology: "these are the moments history says
// look like today, and the odds come from what happened after each one."
export function PriceChart({ symbol }: { symbol: string }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<IChartApi | null>(null);
  const seriesRef = useRef<ISeriesApi<"Area"> | null>(null);

  const { theme } = useTheme();
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);
  const [markerCount, setMarkerCount] = useState(0);

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

    Promise.all([api.getCandles(symbol), api.getHistoricalMatches(40)])
      .then(([candles, matches]) => {
        if (cancelled || candles.length === 0) return;

        const points = candles.map((c) => ({
          time: c.openTime.slice(0, 10) as Time,
          value: c.close,
        }));
        series.setData(points);

        // Mark analog dates that fall inside this asset's price history
        const first = candles[0].openTime.slice(0, 10);
        const last = candles[candles.length - 1].openTime.slice(0, 10);

        const markers: SeriesMarker<Time>[] = matches
          .map((m) => m.date.slice(0, 10))
          .filter((d) => d >= first && d <= last)
          .sort()
          .map((d) => ({
            time: d as Time,
            position: "belowBar" as const,
            color: "#f59e0b",
            shape: "circle" as const,
            size: 1,
          }));

        series.setMarkers(markers);
        setMarkerCount(markers.length);
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
        {markerCount > 0 && (
          <p className="flex items-center gap-1.5 text-xs text-zinc-500">
            <span className="inline-block h-2 w-2 rounded-full bg-amber-500" />
            macro conditions resembled today&apos;s ({markerCount} periods shown)
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

      {markerCount > 0 && (
        <p className="mt-2 text-xs text-zinc-500">
          Each dot marks a month whose macro environment most closely resembled today&apos;s.
          The odds on this page are computed from what {symbol} did after those moments.
        </p>
      )}
    </div>
  );
}
