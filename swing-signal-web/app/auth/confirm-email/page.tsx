"use client";

import { useEffect, useState, Suspense } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { api } from "@/lib/api";
import { MailCheck, MailX, Loader2 } from "lucide-react";

function ConfirmEmailContent() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") ?? "";

  // A missing token is known at first render — no effect needed for that case
  const [status, setStatus] = useState<"working" | "success" | "error">(token ? "working" : "error");
  const [message, setMessage] = useState(token ? "" : "This page needs a confirmation link from your email.");

  useEffect(() => {
    if (!token) return;

    let cancelled = false;
    api
      .confirmEmail(token)
      .then(() => {
        if (!cancelled) setStatus("success");
      })
      .catch(() => {
        if (cancelled) return;
        setStatus("error");
        setMessage("This confirmation link is invalid or has expired.");
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  if (status === "working") {
    return (
      <div className="text-center">
        <Loader2 className="mx-auto h-8 w-8 animate-spin text-zinc-500" />
        <p className="mt-3 text-sm text-zinc-500">Confirming your email...</p>
      </div>
    );
  }

  if (status === "success") {
    return (
      <div className="rounded-xl border border-emerald-500/30 bg-white dark:bg-zinc-900 p-6 text-center">
        <MailCheck className="mx-auto h-8 w-8 text-emerald-600 dark:text-emerald-400" />
        <h2 className="mt-3 text-lg font-semibold text-zinc-900 dark:text-white">Email confirmed!</h2>
        <p className="mt-1 text-sm text-zinc-600 dark:text-zinc-400">
          Your account is fully set up. Welcome aboard.
        </p>
        <Link
          href="/dashboard"
          className="mt-4 inline-block rounded-lg bg-emerald-600 px-6 py-2.5 font-medium text-white transition-colors hover:bg-emerald-500"
        >
          Open the dashboard
        </Link>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-6 text-center">
      <MailX className="mx-auto h-8 w-8 text-amber-600 dark:text-amber-400" />
      <p className="mt-3 text-sm text-zinc-600 dark:text-zinc-400">{message}</p>
      <p className="mt-2 text-xs text-zinc-500">
        Log in and we&apos;ll offer to send you a fresh link.
      </p>
      <Link
        href="/auth"
        className="mt-4 inline-block rounded-lg bg-emerald-600 px-6 py-2.5 font-medium text-white transition-colors hover:bg-emerald-500"
      >
        Go to login
      </Link>
    </div>
  );
}

export default function ConfirmEmailPage() {
  return (
    <div className="mx-auto flex max-w-sm flex-col py-16">
      <Suspense>
        <ConfirmEmailContent />
      </Suspense>
    </div>
  );
}
