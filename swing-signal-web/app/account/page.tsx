"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/context/AuthContext";
import type { UserProfileDto } from "@/types";
import {
  UserCircle, Loader2, MailCheck, MailWarning, KeyRound, Trash2, Check,
} from "lucide-react";

export default function AccountPage() {
  const { user, loading: authLoading, logout } = useAuth();
  const router = useRouter();

  const [profile, setProfile] = useState<UserProfileDto | null>(null);
  const [loading, setLoading] = useState(true);

  // change password form
  const [currentPw, setCurrentPw] = useState("");
  const [newPw, setNewPw] = useState("");
  const [confirmPw, setConfirmPw] = useState("");
  const [pwState, setPwState] = useState<"idle" | "busy" | "done">("idle");
  const [pwError, setPwError] = useState<string | null>(null);

  // resend confirmation
  const [resendState, setResendState] = useState<"idle" | "busy" | "sent">("idle");

  // delete account
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (authLoading) return;
    if (!user) {
      router.push("/auth?returnTo=/account");
      return;
    }

    let cancelled = false;
    api
      .getProfile()
      .then((p) => {
        if (!cancelled) setProfile(p);
      })
      .catch(() => {})
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [authLoading, user, router]);

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (newPw !== confirmPw) {
      setPwError("New passwords don't match.");
      return;
    }
    setPwState("busy");
    setPwError(null);
    try {
      await api.changePassword(currentPw, newPw);
      setPwState("done");
      setCurrentPw("");
      setNewPw("");
      setConfirmPw("");
    } catch (err) {
      setPwState("idle");
      setPwError(
        err instanceof ApiError && err.message && !err.message.startsWith("API error")
          ? err.message.replace(/^"|"$/g, "")
          : "Could not update the password."
      );
    }
  };

  const handleResend = async () => {
    if (!profile) return;
    setResendState("busy");
    try {
      await api.resendConfirmation(profile.email);
      setResendState("sent");
    } catch {
      setResendState("idle");
    }
  };

  const handleDelete = async () => {
    setDeleting(true);
    try {
      await api.deleteAccount();
      logout();
      router.push("/");
    } catch {
      setDeleting(false);
      setConfirmingDelete(false);
    }
  };

  if (authLoading || loading) {
    return (
      <div className="flex justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-zinc-600" />
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="py-24 text-center text-sm text-zinc-500">
        Could not load your account. <Link href="/auth" className="text-emerald-600 dark:text-emerald-400">Try logging in again.</Link>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-xl space-y-6 px-4 py-10">
      <div className="text-center">
        <UserCircle className="mx-auto mb-2 h-9 w-9 text-emerald-600 dark:text-emerald-400" />
        <h1 className="text-2xl font-bold text-zinc-900 dark:text-white">Your Account</h1>
      </div>

      {/* Profile */}
      <section className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p className="font-medium text-zinc-900 dark:text-white">{profile.email}</p>
            <p className="mt-0.5 text-xs text-zinc-500">
              Member since{" "}
              {new Date(profile.createdAt).toLocaleDateString("en-US", {
                year: "numeric",
                month: "long",
              })}
            </p>
          </div>
          <span className="rounded-md border border-zinc-300 dark:border-zinc-700 bg-zinc-100 dark:bg-zinc-800 px-2.5 py-1 text-xs font-medium text-zinc-700 dark:text-zinc-300">
            {profile.plan} plan
          </span>
        </div>

        <div className="mt-4 border-t border-zinc-200 dark:border-zinc-800 pt-4">
          {profile.isEmailConfirmed ? (
            <p className="flex items-center gap-2 text-sm text-emerald-700 dark:text-emerald-400">
              <MailCheck className="h-4 w-4" /> Email confirmed
            </p>
          ) : (
            <div className="flex flex-wrap items-center gap-3">
              <p className="flex items-center gap-2 text-sm text-amber-600 dark:text-amber-400">
                <MailWarning className="h-4 w-4" /> Email not confirmed — unconfirmed accounts are
                deleted after 7 days
              </p>
              {resendState === "sent" ? (
                <span className="flex items-center gap-1 text-xs text-emerald-600 dark:text-emerald-400">
                  <Check className="h-3.5 w-3.5" /> Sent — check your inbox
                </span>
              ) : (
                <button
                  onClick={handleResend}
                  disabled={resendState === "busy"}
                  className="rounded-lg border border-zinc-300 dark:border-zinc-700 px-3 py-1 text-xs font-medium text-zinc-700 dark:text-zinc-300 transition-colors hover:border-zinc-400 dark:hover:border-zinc-500 disabled:opacity-50"
                >
                  Resend confirmation email
                </button>
              )}
            </div>
          )}
        </div>
      </section>

      {/* Change password */}
      <section className="rounded-xl border border-zinc-200 dark:border-zinc-800 bg-white dark:bg-zinc-900 p-5">
        <h2 className="mb-1 flex items-center gap-2 font-semibold text-zinc-900 dark:text-white">
          <KeyRound className="h-4 w-4 text-zinc-500" /> Change password
        </h2>
        <p className="mb-4 text-xs text-zinc-500">
          Signed up with Google and never set a password? Use{" "}
          <Link href="/auth/forgot-password" className="text-emerald-600 dark:text-emerald-400 hover:underline">
            password reset
          </Link>{" "}
          to create one first.
        </p>

        {pwState === "done" ? (
          <p className="flex items-center gap-2 text-sm text-emerald-700 dark:text-emerald-400">
            <Check className="h-4 w-4" /> Password updated. Other sessions were signed out.
          </p>
        ) : (
          <form onSubmit={handleChangePassword} className="space-y-3">
            <input
              type="password"
              required
              value={currentPw}
              onChange={(e) => setCurrentPw(e.target.value)}
              placeholder="Current password"
              className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
            />
            <input
              type="password"
              required
              minLength={8}
              value={newPw}
              onChange={(e) => setNewPw(e.target.value)}
              placeholder="New password (min. 8 characters)"
              className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
            />
            <input
              type="password"
              required
              minLength={8}
              value={confirmPw}
              onChange={(e) => setConfirmPw(e.target.value)}
              placeholder="Repeat new password"
              className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-950 px-3 py-2 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
            />
            {pwError && <p className="text-sm text-red-600 dark:text-red-400">{pwError}</p>}
            <button
              type="submit"
              disabled={pwState === "busy"}
              className="flex items-center gap-2 rounded-lg bg-emerald-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-emerald-500 disabled:opacity-50"
            >
              {pwState === "busy" && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
              Update password
            </button>
          </form>
        )}
      </section>

      {/* Danger zone */}
      <section className="rounded-xl border border-red-300/50 dark:border-red-900/50 bg-white dark:bg-zinc-900 p-5">
        <h2 className="mb-1 flex items-center gap-2 font-semibold text-red-700 dark:text-red-400">
          <Trash2 className="h-4 w-4" /> Delete account
        </h2>
        <p className="mb-3 text-xs text-zinc-500">
          Permanently removes your account, email, and personal data. Your anonymous
          usage history is kept without any link to you. This cannot be undone.
        </p>

        {!confirmingDelete ? (
          <button
            onClick={() => setConfirmingDelete(true)}
            className="rounded-lg border border-red-400/50 px-3.5 py-1.5 text-sm font-medium text-red-700 dark:text-red-400 transition-colors hover:bg-red-500/10"
          >
            Delete my account
          </button>
        ) : (
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium text-zinc-900 dark:text-white">Are you sure?</span>
            <button
              onClick={handleDelete}
              disabled={deleting}
              className="flex items-center gap-2 rounded-lg bg-red-600 px-3.5 py-1.5 text-sm font-medium text-white transition-colors hover:bg-red-500 disabled:opacity-50"
            >
              {deleting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
              Yes, delete everything
            </button>
            <button
              onClick={() => setConfirmingDelete(false)}
              disabled={deleting}
              className="text-sm text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300"
            >
              Cancel
            </button>
          </div>
        )}
      </section>
    </div>
  );
}
