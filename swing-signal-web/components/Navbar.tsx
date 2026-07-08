import Link from "next/link";
import { TrendingUp } from "lucide-react";
import { UserMenu } from "@/components/UserMenu";
import { ThemeToggle } from "@/components/ThemeToggle";

export function Navbar() {
  return (
    <nav className="sticky top-0 z-50 border-b border-zinc-200 dark:border-zinc-800 bg-white/90 dark:bg-zinc-950/90 backdrop-blur">
      <div className="mx-auto flex h-14 max-w-7xl items-center gap-4 px-4">
        <Link href="/" className="flex items-center gap-2">
          <TrendingUp className="h-5 w-5 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <span className="flex flex-col leading-tight">
            <span className="font-bold text-zinc-900 dark:text-white">SwingSignal</span>
            <span className="hidden text-[10px] text-zinc-500 dark:text-zinc-500 sm:block">
              Macro Context Dashboard
            </span>
          </span>
        </Link>

        <div className="ml-auto flex items-center gap-4">
          {/* Journey order: act (Dashboard) → watch (Live Macro) → learn → look up */}
          {[
            { href: "/dashboard", label: "Dashboard" },
            { href: "/macro", label: "Live Macro" },
            { href: "/indicators", label: "Learn" },
            { href: "/glossary", label: "Dictionary" },
          ].map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className="text-sm font-medium text-zinc-700 dark:text-zinc-300 transition-colors hover:text-zinc-900 dark:hover:text-white"
            >
              {item.label}
            </Link>
          ))}
          <ThemeToggle />
          <UserMenu />
        </div>
      </div>
    </nav>
  );
}
