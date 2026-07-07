"use client";

import { useState, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { GoogleLogin } from "@react-oauth/google";
import { useAuth } from "@/context/AuthContext";
import { ApiError } from "@/lib/api";
import { TrendingUp, Loader2 } from "lucide-react";

const GOOGLE_ENABLED = Boolean(process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID);

function AuthForm() {
  const [mode, setMode] = useState<"login" | "register">("register");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const { login, register, googleLogin } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnTo = searchParams.get("returnTo") ?? "/dashboard";

  const handleGoogle = async (idToken?: string) => {
    if (!idToken) {
      setError("Google sign-in failed. Please try again.");
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await googleLogin(idToken);
      router.push(returnTo);
    } catch {
      setError("Google sign-in failed. Please try again or use email.");
    } finally {
      setBusy(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (mode === "login") await login(email, password);
      else await register(email, password);
      router.push(returnTo);
    } catch (err) {
      setError(
        err instanceof ApiError && err.message && !err.message.startsWith("API error")
          ? err.message.replace(/^"|"$/g, "")
          : mode === "login"
          ? "Invalid email or password."
          : "Registration failed. Try a different email."
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto flex max-w-sm flex-col py-16">
      <div className="mb-8 text-center">
        <TrendingUp className="mx-auto h-8 w-8 text-emerald-600 dark:text-emerald-400" />
        <h1 className="mt-3 text-2xl font-bold text-zinc-900 dark:text-white">
          {mode === "register" ? "Create your free account" : "Welcome back"}
        </h1>
        <p className="mt-1 text-sm text-zinc-500">
          {mode === "register"
            ? "Analyze any symbol with custom prediction windows."
            : "Log in to continue."}
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <input
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="Email"
          className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-900 px-4 py-2.5 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
        />
        <input
          type="password"
          required
          minLength={8}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          placeholder="Password (min. 8 characters)"
          className="w-full rounded-lg border border-zinc-300 dark:border-zinc-700 bg-white dark:bg-zinc-900 px-4 py-2.5 text-sm text-zinc-900 dark:text-zinc-100 placeholder-zinc-500 outline-none focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500"
        />

        {error && <p className="text-sm text-red-600 dark:text-red-400">{error}</p>}

        <button
          type="submit"
          disabled={busy}
          className="flex w-full items-center justify-center gap-2 rounded-lg bg-emerald-600 py-2.5 font-medium text-white transition-colors hover:bg-emerald-500 disabled:opacity-50"
        >
          {busy && <Loader2 className="h-4 w-4 animate-spin" />}
          {mode === "register" ? "Create account" : "Log in"}
        </button>
      </form>

      {GOOGLE_ENABLED && (
        <>
          <div className="my-5 flex items-center gap-3">
            <div className="h-px flex-1 bg-zinc-300 dark:bg-zinc-700" />
            <span className="text-xs text-zinc-500">or</span>
            <div className="h-px flex-1 bg-zinc-300 dark:bg-zinc-700" />
          </div>
          <div className="flex justify-center">
            <GoogleLogin
              onSuccess={(cred) => handleGoogle(cred.credential)}
              onError={() => setError("Google sign-in failed. Please try again.")}
              theme="filled_black"
              text={mode === "register" ? "signup_with" : "signin_with"}
              width="320"
            />
          </div>
        </>
      )}

      <button
        onClick={() => {
          setMode(mode === "login" ? "register" : "login");
          setError(null);
        }}
        className="mt-6 text-sm text-zinc-500 transition-colors hover:text-zinc-700 dark:hover:text-zinc-300"
      >
        {mode === "register"
          ? "Already have an account? Log in"
          : "New here? Create a free account"}
      </button>
    </div>
  );
}

export default function AuthPage() {
  return (
    <Suspense>
      <AuthForm />
    </Suspense>
  );
}
