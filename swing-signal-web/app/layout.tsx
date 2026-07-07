import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import { Navbar } from "@/components/Navbar";
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

export const metadata: Metadata = {
  title: "SwingSignal — Macro Context Dashboard",
  description:
    "Historical odds of asset price movements given current macro conditions.",
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
      </head>
      <body className="min-h-full flex flex-col bg-zinc-50 text-zinc-900 dark:bg-zinc-950 dark:text-zinc-100">
        <GoogleProvider>
          <AuthProvider>
            <ThemeProvider>
              <Navbar />
              <main className="flex-1">{children}</main>
              <footer className="border-t border-zinc-200 py-4 text-center text-xs text-zinc-500 dark:border-zinc-800 dark:text-zinc-600">
                Historical data only. Not financial advice.
              </footer>
            </ThemeProvider>
          </AuthProvider>
        </GoogleProvider>
      </body>
    </html>
  );
}
