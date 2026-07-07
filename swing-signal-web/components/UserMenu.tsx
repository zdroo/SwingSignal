"use client";

import Link from "next/link";
import { useAuth } from "@/context/AuthContext";
import { LogOut, User } from "lucide-react";

export function UserMenu() {
  const { user, loading, logout } = useAuth();

  if (loading) return <div className="h-8 w-20" />;

  if (!user) {
    return (
      <Link
        href="/auth"
        className="rounded-lg bg-emerald-600 px-3.5 py-1.5 text-sm font-medium text-white transition-colors hover:bg-emerald-500"
      >
        Sign up free
      </Link>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <span className="hidden items-center gap-1.5 text-sm text-zinc-600 dark:text-zinc-400 sm:flex">
        <User className="h-3.5 w-3.5" />
        {user.email}
        {user.plan === "Pro" && (
          <span className="rounded bg-emerald-500/10 px-1.5 py-0.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
            Pro
          </span>
        )}
      </span>
      <button
        onClick={logout}
        title="Log out"
        className="text-zinc-500 transition-colors hover:text-zinc-700 dark:hover:text-zinc-300"
      >
        <LogOut className="h-4 w-4" />
      </button>
    </div>
  );
}
