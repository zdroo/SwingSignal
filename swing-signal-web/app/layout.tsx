import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { Navbar } from "@/components/Navbar";
import { AnalyticsScript } from "@/components/AnalyticsScript";
import { GoogleProvider } from "@/components/GoogleProvider";
import { AuthProvider } from "@/context/AuthContext";
import { ThemeProvider } from "@/context/ThemeContext";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "http://localhost:3000";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: {
    default: "SwingSignal — Honest Odds for Swing Traders",
    template: "%s — SwingSignal",
  },
  description:
    "See how assets historically performed in macro conditions like today's. Honest, backtested odds for stocks, crypto, forex and commodities — no signals, no promises.",
  keywords: [
    "macro indicators", "swing trading", "market regime", "historical odds",
    "yield curve", "fed funds rate", "backtested", "bitcoin odds", "S&P 500 odds",
  ],
  openGraph: {
    type: "website",
    siteName: "SwingSignal",
    title: "SwingSignal — Honest Odds for Swing Traders",
    description:
      "Historical odds for any asset, based on 30+ years of macro regimes. Verify our accuracy yourself — every asset page has a built-in backtest.",
    url: SITE_URL,
  },
  twitter: {
    card: "summary_large_image",
    title: "SwingSignal — Honest Odds for Swing Traders",
    description:
      "Historical odds for any asset, based on 30+ years of macro regimes. No signals, no promises — and you can verify our accuracy yourself.",
  },
  robots: {
    index: true,
    follow: true,
  },
};

// Search engines: what this site is
const jsonLd = {
  "@context": "https://schema.org",
  "@type": "WebSite",
  name: "SwingSignal",
  url: SITE_URL,
  description:
    "Macro context dashboard for swing traders: historical odds of asset price movements given current macroeconomic conditions.",
  publisher: {
    "@type": "Organization",
    name: "SwingSignal",
    url: SITE_URL,
  },
};

// Runs before hydration so the correct theme class is present on first paint
const themeInitScript = `
try {
  var t = localStorage.getItem("ss_theme");
  if (t !== "light") document.documentElement.classList.add("dark");
} catch (e) { document.documentElement.classList.add("dark"); }
`;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="en"
      suppressHydrationWarning
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}
    >
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeInitScript }} />
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
        />
        <AnalyticsScript />
      </head>
      <body className="min-h-full flex flex-col bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
        <GoogleProvider>
          <AuthProvider>
            <ThemeProvider>
              <Navbar />
              <main className="flex-1">{children}</main>
              <footer className="border-t border-zinc-200 py-4 text-center text-xs text-zinc-500 dark:border-zinc-800 dark:text-zinc-600">
                <p>Historical data only. Not financial advice.</p>
                <p className="mt-1.5 space-x-3">
                  <a href="/disclaimer" className="hover:text-zinc-700 dark:hover:text-zinc-400">Disclaimer</a>
                  <a href="/terms" className="hover:text-zinc-700 dark:hover:text-zinc-400">Terms</a>
                  <a href="/privacy" className="hover:text-zinc-700 dark:hover:text-zinc-400">Privacy</a>
                </p>
              </footer>
            </ThemeProvider>
          </AuthProvider>
        </GoogleProvider>
      </body>
    </html>
  );
}
