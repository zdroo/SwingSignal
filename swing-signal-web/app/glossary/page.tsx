import Link from "next/link";
import type { Metadata } from "next";
import { GlossaryList } from "@/components/GlossaryList";
import { GraduationCap, ArrowRight } from "lucide-react";

export const metadata: Metadata = {
  title: "Investing Dictionary for Beginners",
  description:
    "Inflation, yield, bonds, leverage, the Fed — every investing term explained in plain language with everyday analogies. Written for people just getting started.",
  alternates: { canonical: "/glossary" },
};

export default function GlossaryPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <div className="mb-10 text-center">
        <GraduationCap className="mx-auto mb-3 h-8 w-8 text-emerald-600 dark:text-emerald-400" />
        <h1 className="text-3xl font-bold text-zinc-900 dark:text-white">
          The Investing Dictionary
        </h1>
        <p className="mx-auto mt-3 max-w-xl leading-relaxed text-zinc-600 dark:text-zinc-400">
          Every term you&apos;ll run into here, explained in normal language — no
          finance degree required. If you&apos;re just starting out, this page is
          for you.
        </p>
      </div>

      <GlossaryList />

      <div className="border-t border-zinc-200 dark:border-zinc-800 pt-8 text-center">
        <p className="mb-4 text-sm text-zinc-600 dark:text-zinc-400">
          Ready for the next level? Learn what the macro indicators mean.
        </p>
        <Link
          href="/indicators"
          className="inline-flex items-center gap-2 rounded-lg bg-emerald-600 px-6 py-3 font-medium text-white transition-colors hover:bg-emerald-500"
        >
          Understanding the indicators
          <ArrowRight className="h-4 w-4" />
        </Link>
      </div>
    </div>
  );
}
