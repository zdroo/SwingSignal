import Link from "next/link";
import type { Metadata } from "next";
import { FAMILIES, INDICATORS, COMBOS, MOST_WATCHED, getIndicator } from "@/lib/indicators";
import { ArrowRight, BookOpen, Layers, Star } from "lucide-react";

export const metadata: Metadata = {
  title: "Understanding Macro Indicators",
  description:
    "The yield curve, Fed funds rate, VIX, credit spreads and 20+ more macro indicators explained: what they measure, what high and low values mean, and how professionals read them in combination.",
  alternates: { canonical: "/indicators" },
};

function IndicatorEntry({ indicatorKey }: { indicatorKey: string }) {
  const info = getIndicator(indicatorKey)!;

  return (
    <article
      id={info.key}
      className="scroll-mt-20 rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-6"
    >
      <div className="flex items-baseline justify-between gap-3">
        <h3 className="text-lg font-semibold text-zinc-900 dark:text-white">{info.name}</h3>
        {info.unit && <span className="text-xs text-zinc-500">measured in {info.unit}</span>}
      </div>

      <p className="mt-2 leading-relaxed text-zinc-600 dark:text-zinc-400">{info.short}</p>
      <p className="mt-2 text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">{info.detail}</p>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <div className="rounded-lg border border-emerald-400/30 bg-emerald-400/5 p-3">
          <p className="text-xs font-semibold uppercase tracking-wider text-emerald-600 dark:text-emerald-400">
            When it&apos;s low
          </p>
          <p className="mt-1 text-sm leading-relaxed text-zinc-600 dark:text-zinc-300">{info.low}</p>
        </div>
        <div className="rounded-lg border border-amber-400/30 bg-amber-400/5 p-3">
          <p className="text-xs font-semibold uppercase tracking-wider text-amber-600 dark:text-amber-400">
            When it&apos;s high
          </p>
          <p className="mt-1 text-sm leading-relaxed text-zinc-600 dark:text-zinc-300">{info.high}</p>
        </div>
      </div>

      <p className="mt-3 text-xs text-zinc-500">
        <span className="font-medium">Signal bands: </span>
        {info.bands}
      </p>
    </article>
  );
}

export default function IndicatorsPage() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-10">
      <div className="mb-10 text-center">
        <BookOpen className="mx-auto mb-3 h-8 w-8 text-emerald-600 dark:text-emerald-400" />
        <h1 className="text-3xl font-bold text-zinc-900 dark:text-white">
          Understanding the Indicators
        </h1>
        <p className="mx-auto mt-3 max-w-2xl leading-relaxed text-zinc-600 dark:text-zinc-400">
          Every card on the dashboard, explained: what it measures, what high and low
          values have historically meant, and — because no indicator works alone — how
          the important combinations read together.
        </p>
        <p className="mt-3 text-sm text-zinc-500">
          New to investing? Start with the{" "}
          <Link
            href="/glossary"
            className="font-medium text-emerald-600 dark:text-emerald-400 hover:text-emerald-500 dark:hover:text-emerald-300"
          >
            plain-language dictionary
          </Link>{" "}
          — terms like yield, bond, and inflation explained simply.
        </p>
      </div>

      {/* Table of contents: every indicator, grouped by family */}
      <nav className="mb-12 rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-6">
        <div className="mb-5 flex flex-wrap items-center justify-between gap-2">
          <p className="text-xs font-semibold uppercase tracking-wider text-zinc-500">
            All indicators
          </p>
          <p className="flex items-center gap-1.5 text-xs text-zinc-500">
            <Star className="h-3 w-3 fill-emerald-500 text-emerald-500" />
            most watched
          </p>
        </div>

        <div className="grid gap-x-8 gap-y-6 sm:grid-cols-2 lg:grid-cols-3">
          {FAMILIES.map((family) => {
            const members = INDICATORS.filter((i) => i.family === family);
            if (members.length === 0) return null;

            return (
              <div key={family}>
                <a
                  href={`#${family.replace(/[^a-zA-Z]+/g, "-")}`}
                  className="mb-2 block text-sm font-semibold text-zinc-900 dark:text-white hover:text-emerald-600 dark:hover:text-emerald-400"
                >
                  {family}
                </a>
                <div className="flex flex-wrap gap-1.5">
                  {members.map((i) => {
                    const watched = MOST_WATCHED.has(i.key);
                    return (
                      <a
                        key={i.key}
                        href={`#${i.key}`}
                        className={
                          watched
                            ? "inline-flex items-center gap-1 rounded-md border border-emerald-500/50 bg-emerald-400/10 px-2 py-0.5 text-xs font-medium text-emerald-700 dark:text-emerald-400 transition-colors hover:border-emerald-500 hover:bg-emerald-400/20"
                            : "rounded-md border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 px-2 py-0.5 text-xs text-zinc-600 dark:text-zinc-400 transition-colors hover:border-zinc-400 dark:hover:border-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-200"
                        }
                      >
                        {watched && <Star className="h-2.5 w-2.5 fill-emerald-500 text-emerald-500" />}
                        {i.name}
                      </a>
                    );
                  })}
                </div>
              </div>
            );
          })}

          {/* Combinations shortcut as its own block */}
          <div>
            <a
              href="#combinations"
              className="mb-2 block text-sm font-semibold text-zinc-900 dark:text-white hover:text-emerald-600 dark:hover:text-emerald-400"
            >
              Reading Combinations
            </a>
            <div className="flex flex-wrap gap-1.5">
              {COMBOS.map((combo) => (
                <a
                  key={combo.title}
                  href="#combinations"
                  className="rounded-md border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 px-2 py-0.5 text-xs text-zinc-600 dark:text-zinc-400 transition-colors hover:border-zinc-400 dark:hover:border-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-200"
                >
                  {combo.title.split(":")[0]}
                </a>
              ))}
            </div>
          </div>
        </div>
      </nav>

      {/* Indicators by family */}
      {FAMILIES.map((family) => {
        const members = INDICATORS.filter((i) => i.family === family);
        if (members.length === 0) return null;

        return (
          <section key={family} className="mb-12">
            <h2
              id={family.replace(/[^a-zA-Z]+/g, "-")}
              className="mb-4 scroll-mt-20 text-xl font-bold text-zinc-900 dark:text-white"
            >
              {family}
            </h2>
            <div className="space-y-4">
              {members.map((i) => (
                <IndicatorEntry key={i.key} indicatorKey={i.key} />
              ))}
            </div>
          </section>
        );
      })}

      {/* Combinations */}
      <section id="combinations" className="mb-12 scroll-mt-20">
        <div className="mb-6 text-center">
          <Layers className="mx-auto mb-3 h-7 w-7 text-emerald-600 dark:text-emerald-400" />
          <h2 className="text-2xl font-bold text-zinc-900 dark:text-white">
            Reading Combinations
          </h2>
          <p className="mx-auto mt-2 max-w-2xl text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">
            No indicator means much alone. Regimes emerge from combinations — these are
            the pairings that professional macro watchers actually read together.
          </p>
        </div>

        <div className="space-y-4">
          {COMBOS.map((combo) => (
            <article
              key={combo.title}
              className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-6"
            >
              <h3 className="font-semibold text-zinc-900 dark:text-white">{combo.title}</h3>
              <div className="mt-2 flex flex-wrap gap-1.5">
                {combo.indicators.map((key) => {
                  const info = getIndicator(key);
                  return (
                    <a
                      key={key}
                      href={`#${key}`}
                      className="rounded-md border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 px-2 py-0.5 text-xs text-zinc-600 dark:text-zinc-400 hover:border-emerald-500 hover:text-emerald-600 dark:hover:text-emerald-400"
                    >
                      {info?.name ?? key}
                    </a>
                  );
                })}
              </div>
              <p className="mt-3 text-sm leading-relaxed text-zinc-600 dark:text-zinc-400">
                {combo.text}
              </p>
            </article>
          ))}
        </div>
      </section>

      <div className="pb-8 text-center">
        <p className="mb-4 text-sm text-zinc-600 dark:text-zinc-400">
          Now see what these indicators say about today.
        </p>
        <Link
          href="/dashboard"
          className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-6 py-3 font-medium text-white transition-colors hover:bg-emerald-500"
        >
          Open the live dashboard
          <ArrowRight className="h-4 w-4" />
        </Link>
      </div>
    </div>
  );
}
