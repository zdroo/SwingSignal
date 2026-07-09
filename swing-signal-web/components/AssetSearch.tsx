"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { track } from "@/lib/analytics";
import type { SymbolSearchResultDto } from "@/types";
import { Search, Loader2 } from "lucide-react";

const POPULAR = ["BTCUSDT", "ETHUSDT", "SPY", "QQQ", "GLD", "EURUSD=X", "AAPL", "NVDA", "GC=F"];

const TYPE_COLORS: Record<string, string> = {
  Stock:  "text-sky-600 dark:text-sky-400 border-sky-400/30 bg-sky-400/10",
  ETF:    "text-violet-600 dark:text-violet-400 border-violet-400/30 bg-violet-400/10",
  Crypto: "text-amber-600 dark:text-amber-400 border-amber-400/30 bg-amber-400/10",
  Forex:  "text-emerald-600 dark:text-emerald-400 border-emerald-400/30 bg-emerald-400/10",
  Future: "text-orange-600 dark:text-orange-400 border-orange-400/30 bg-orange-400/10",
  Index:  "text-zinc-600 dark:text-zinc-400 border-zinc-400/30 bg-zinc-400/10",
};

export function AssetSearch() {
  const [value, setValue] = useState("");
  const [results, setResults] = useState<SymbolSearchResultDto[]>([]);
  const [open, setOpen] = useState(false);
  const [highlighted, setHighlighted] = useState(-1);
  const [searching, setSearching] = useState(false);
  const [pending, startTransition] = useTransition();

  const router = useRouter();
  const containerRef = useRef<HTMLDivElement>(null);

  // Clearing on short input lives in the change handler; the effect only fetches
  const handleChange = (v: string) => {
    setValue(v);
    if (v.trim().length < 2) {
      setResults([]);
      setOpen(false);
    }
  };

  // Debounced autocomplete lookup
  useEffect(() => {
    const q = value.trim();
    if (q.length < 2) return;

    let cancelled = false;

    const timer = setTimeout(() => {
      setSearching(true);
      api
        .searchSymbols(q)
        .then((r) => {
          if (cancelled) return;
          setResults(r);
          setOpen(r.length > 0);
          setHighlighted(-1);
        })
        .catch(() => {
          if (!cancelled) setResults([]);
        })
        .finally(() => {
          if (!cancelled) setSearching(false);
        });
    }, 250);

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [value]);

  // Close dropdown on outside click
  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, []);

  const navigate = (symbol: string, source: string) => {
    const s = symbol.trim();
    if (!s) return;
    setOpen(false);
    track("search_performed", { symbol: s, source });
    const params = new URLSearchParams({ src: source });
    if (value.trim() && value.trim().toUpperCase() !== s.toUpperCase()) params.set("q", value.trim());
    startTransition(() => {
      router.push(`/odds/${encodeURIComponent(s)}?${params.toString()}`);
    });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (highlighted >= 0 && results[highlighted]) navigate(results[highlighted].symbol, "autocomplete");
    else if (results.length > 0 && open) navigate(results[0].symbol, "autocomplete");
    else navigate(value, "search");
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (!open || results.length === 0) return;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setHighlighted((h) => (h + 1) % results.length);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setHighlighted((h) => (h <= 0 ? results.length - 1 : h - 1));
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  };

  return (
    <div className="space-y-4">
      <form onSubmit={handleSubmit} className="flex gap-2">
        <div ref={containerRef} className="relative flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-zinc-500" />
          <input
            type="text"
            value={value}
            onChange={(e) => handleChange(e.target.value)}
            onKeyDown={handleKeyDown}
            onFocus={() => results.length > 0 && setOpen(true)}
            placeholder="Type a name or symbol — apple, bitcoin, EUR/USD ..."
            className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-900 py-2.5 pl-9 pr-9 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          />
          {searching && (
            <Loader2 className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-zinc-500" />
          )}

          {open && results.length > 0 && (
            <ul className="absolute left-0 right-0 top-full z-40 mt-1 overflow-hidden rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-900 shadow-xl">
              {results.map((r, i) => (
                <li key={`${r.symbol}-${i}`}>
                  <button
                    type="button"
                    onMouseEnter={() => setHighlighted(i)}
                    onClick={() => navigate(r.symbol, "autocomplete")}
                    className={`flex w-full items-center gap-3 px-3 py-2.5 text-left text-sm transition-colors ${
                      i === highlighted
                        ? "bg-zinc-100 dark:bg-zinc-800"
                        : "bg-transparent"
                    }`}
                  >
                    <span className="min-w-20 font-semibold text-zinc-900 dark:text-white">
                      {r.symbol}
                    </span>
                    <span className="flex-1 truncate text-zinc-600 dark:text-zinc-400">
                      {r.name}
                    </span>
                    <span
                      className={`rounded-md border px-1.5 py-0.5 text-[10px] font-medium ${
                        TYPE_COLORS[r.type] ?? TYPE_COLORS.Index
                      }`}
                    >
                      {r.type}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <button
          type="submit"
          disabled={!value.trim() || pending}
          className="flex items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-emerald-500 disabled:opacity-50"
        >
          {pending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
          Analyze
        </button>
      </form>

      <div className="flex flex-wrap gap-2">
        <span className="text-xs text-zinc-500 dark:text-zinc-600 self-center">Popular:</span>
        {POPULAR.map((s) => (
          <button
            key={s}
            onClick={() => navigate(s, "popular")}
            className="rounded-md border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 px-2.5 py-1 text-xs text-zinc-600 dark:text-zinc-400 transition-colors hover:border-zinc-400 dark:hover:border-zinc-600 hover:text-zinc-800 dark:hover:text-zinc-200"
          >
            {s}
          </button>
        ))}
      </div>
    </div>
  );
}
