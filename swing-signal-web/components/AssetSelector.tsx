"use client";

import { useRouter } from "next/navigation";
import type { AssetDto } from "@/types";

const MARKET_ORDER = ["Crypto", "Stock", "Index", "Forex", "Commodity"];

interface Props {
  assets: AssetDto[];
  current?: string;
}

export function AssetSelector({ assets, current }: Props) {
  const router = useRouter();

  const grouped = MARKET_ORDER.reduce<Record<string, AssetDto[]>>((acc, type) => {
    acc[type] = assets.filter((a) => a.marketType === type);
    return acc;
  }, {});

  return (
    <div className="flex flex-wrap gap-2">
      {MARKET_ORDER.map((type) =>
        grouped[type].map((a) => (
          <button
            key={a.id}
            onClick={() => router.push(`/odds/${encodeURIComponent(a.symbol)}`)}
            className={`rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors ${
              a.symbol === current
                ? "border-emerald-500 bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                : "border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 text-zinc-700 dark:text-zinc-300 hover:border-zinc-400 dark:hover:border-zinc-600 hover:text-zinc-900 dark:hover:text-white"
            }`}
          >
            {a.symbol}
          </button>
        ))
      )}
    </div>
  );
}
