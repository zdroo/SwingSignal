"use client";

import { useState } from "react";
import { api } from "@/lib/api";
import { track } from "@/lib/analytics";
import { useAuth } from "@/context/AuthContext";
import { Sparkles, Loader2, Check } from "lucide-react";

// "Pro coming soon" interest capture — the cheapest validation of the
// monetization plan. Prefills the email for logged-in users.
export function ProWaitlist({ source }: { source: string }) {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const [email, setEmail] = useState("");
  const [state, setState] = useState<"idle" | "busy" | "done">("idle");
  const [error, setError] = useState<string | null>(null);

  const handleOpen = () => {
    track("waitlist_click", { source });
    setEmail(user?.email ?? "");
    setOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setState("busy");
    setError(null);
    try {
      await api.joinWaitlist(email, source);
      setState("done");
      track("waitlist_joined", { source });
    } catch {
      setState("idle");
      setError("Something went wrong — please try again.");
    }
  };

  if (state === "done") {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-emerald-500/30 bg-emerald-400/5 px-4 py-3 text-sm text-emerald-700 dark:text-emerald-400">
        <Check className="h-4 w-4 shrink-0" />
        You&apos;re on the list — we&apos;ll email you when Pro launches.
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-start gap-2.5">
          <Sparkles className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
          <div>
            <p className="text-sm font-semibold text-zinc-900 dark:text-white">
              Pro is coming: alerts, watchlists &amp; unlimited backtests
            </p>
            <p className="text-xs text-zinc-500">
              Get notified when the regime shifts or your asset&apos;s edge turns positive.
            </p>
          </div>
        </div>

        {!open && (
          <button
            onClick={handleOpen}
            className="rounded-lg border border-emerald-500/50 bg-emerald-400/10 px-3.5 py-1.5 text-sm font-medium text-emerald-700 dark:text-emerald-400 transition-colors hover:bg-emerald-400/20"
          >
            Join the waitlist
          </button>
        )}
      </div>

      {open && (
        <form onSubmit={handleSubmit} className="mt-3 flex gap-2">
          <input
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="Your email"
            className="flex-1 rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
          />
          <button
            type="submit"
            disabled={state === "busy"}
            className="flex items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-emerald-500 disabled:opacity-50"
          >
            {state === "busy" && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
            Notify me
          </button>
        </form>
      )}

      {error && <p className="mt-2 text-xs text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}
