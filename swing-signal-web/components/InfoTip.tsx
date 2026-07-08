import { Info } from "lucide-react";
import type { ReactNode } from "react";

// CSS-only hover balloon behind an info icon. Keeps sections readable: one
// icon next to the title, the explanation on demand. Server-compatible (no
// JS). Don't use inside overflow-x containers — the balloon clips there; use
// a native `title` attribute instead (see OddsTable).
export function InfoTip({
  children,
  align = "center",
}: {
  children: ReactNode;
  align?: "left" | "center" | "right";
}) {
  const pos =
    align === "left"
      ? "left-0"
      : align === "right"
      ? "right-0"
      : "left-1/2 -translate-x-1/2";

  return (
    <span className="group relative inline-flex align-middle">
      <Info tabIndex={0} className="h-3.5 w-3.5 cursor-help text-zinc-400 outline-none" />
      <span
        className={`pointer-events-none absolute top-full z-30 mt-1.5 w-80 max-w-[85vw] rounded-xl border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-800 p-4 text-left text-xs font-normal normal-case leading-relaxed tracking-normal text-zinc-600 dark:text-zinc-300 opacity-0 shadow-xl transition-opacity duration-150 group-hover:opacity-100 group-focus-within:opacity-100 ${pos}`}
      >
        {children}
      </span>
    </span>
  );
}
