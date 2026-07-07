import type { Metadata } from "next";

// The page itself is a client component; metadata lives here so every
// asset page gets a real title/description/OG for search and sharing.
export async function generateMetadata({
  params,
}: {
  params: Promise<{ symbol: string }>;
}): Promise<Metadata> {
  const { symbol } = await params;
  const decoded = decodeURIComponent(symbol).toUpperCase();

  const title = `${decoded} — Historical Odds & Macro Context`;
  const description =
    `What are the odds ${decoded} rises in the next 1-6 months? ` +
    `See how it performed in past macro conditions like today's — price targets, ` +
    `plain-English reasoning, and a backtest you can run yourself.`;

  return {
    title,
    description,
    alternates: { canonical: `/odds/${encodeURIComponent(decoded)}` },
    openGraph: { title, description },
    twitter: { title, description },
  };
}

export default function OddsLayout({ children }: { children: React.ReactNode }) {
  return children;
}
