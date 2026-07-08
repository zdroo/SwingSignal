"use client";

import { useMemo, useState } from "react";
import { GLOSSARY, GLOSSARY_THEMES } from "@/lib/glossary";
import { Search, Lightbulb } from "lucide-react";

export function GlossaryList() {
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return GLOSSARY;
    return GLOSSARY.filter(
      (t) =>
        t.term.toLowerCase().includes(q) ||
        t.definition.toLowerCase().includes(q) ||
        (t.analogy?.toLowerCase().includes(q) ?? false)
    );
  }, [query]);

  return (
    <div>
      <div className="relative mx-auto mb-10 max-w-md">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-zinc-500" />
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search a term... inflation, yield, bond"
          className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-900 py-2.5 pl-9 pr-4 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
        />
      </div>

      {filtered.length === 0 && (
        <p className="text-center text-sm text-zinc-500">
          No terms match &quot;{query}&quot; — try a simpler word.
        </p>
      )}

      {GLOSSARY_THEMES.map((theme) => {
        const terms = filtered.filter((t) => t.theme === theme);
        if (terms.length === 0) return null;

        return (
          <section key={theme} className="mb-10">
            <h2 className="mb-4 text-xl font-bold text-zinc-900 dark:text-white">{theme}</h2>
            <div className="space-y-3">
              {terms.map((t) => (
                <article
                  key={t.term}
                  className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-5"
                >
                  <h3 className="font-semibold text-zinc-900 dark:text-white">{t.term}</h3>
                  <p className="mt-1.5 text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">
                    {t.definition}
                  </p>
                  {t.analogy && (
                    <p className="mt-2.5 flex gap-2 rounded-lg border border-emerald-400/20 bg-emerald-400/5 p-3 text-sm leading-relaxed text-zinc-600 dark:text-zinc-300">
                      <Lightbulb className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
                      <span>{t.analogy}</span>
                    </p>
                  )}
                </article>
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}
