import type { MetadataRoute } from "next";

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

// Asset pages worth indexing — the flagship + popular symbols
const INDEXED_SYMBOLS = [
  "BTCUSDT", "ETHUSDT", "SPY", "QQQ", "GLD", "GC=F", "EURUSD=X", "AAPL", "NVDA",
];

export default function sitemap(): MetadataRoute.Sitemap {
  const now = new Date();

  const staticPages: MetadataRoute.Sitemap = [
    { url: `${SITE_URL}/`, lastModified: now, changeFrequency: "daily", priority: 1 },
    { url: `${SITE_URL}/dashboard`, lastModified: now, changeFrequency: "daily", priority: 0.9 },
    { url: `${SITE_URL}/macro`, lastModified: now, changeFrequency: "daily", priority: 0.8 },
    { url: `${SITE_URL}/indicators`, lastModified: now, changeFrequency: "monthly", priority: 0.8 },
    { url: `${SITE_URL}/glossary`, lastModified: now, changeFrequency: "monthly", priority: 0.8 },
    { url: `${SITE_URL}/disclaimer`, lastModified: now, changeFrequency: "yearly", priority: 0.2 },
    { url: `${SITE_URL}/terms`, lastModified: now, changeFrequency: "yearly", priority: 0.2 },
    { url: `${SITE_URL}/privacy`, lastModified: now, changeFrequency: "yearly", priority: 0.2 },
  ];

  const assetPages: MetadataRoute.Sitemap = INDEXED_SYMBOLS.map((symbol) => ({
    url: `${SITE_URL}/odds/${encodeURIComponent(symbol)}`,
    lastModified: now,
    changeFrequency: "daily",
    priority: 0.7,
  }));

  return [...staticPages, ...assetPages];
}
