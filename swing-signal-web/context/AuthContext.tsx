"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { track } from "@/lib/analytics";
import type { AuthResponse } from "@/types";

interface AuthUser {
  email: string;
  plan: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  googleLogin: (idToken: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const TOKEN_KEY = "ss_access_token";
const REFRESH_KEY = "ss_refresh_token";
const EXPIRY_KEY = "ss_token_expiry";
const USER_KEY = "ss_user";

function storeSession(auth: AuthResponse) {
  localStorage.setItem(TOKEN_KEY, auth.accessToken);
  localStorage.setItem(REFRESH_KEY, auth.refreshToken);
  localStorage.setItem(EXPIRY_KEY, auth.accessTokenExpiry);
  localStorage.setItem(USER_KEY, JSON.stringify({ email: auth.email, plan: auth.plan }));
}

function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
  localStorage.removeItem(EXPIRY_KEY);
  localStorage.removeItem(USER_KEY);
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  // Deliberate post-mount hydration: restoring the session in a lazy
  // initializer would make the first client render (logged-in navbar) differ
  // from the server-rendered HTML and trip React's hydration mismatch.
  /* eslint-disable react-hooks/set-state-in-effect -- SSR-safe session hydration, see above */
  useEffect(() => {
    const stored = localStorage.getItem(USER_KEY);
    const expiry = localStorage.getItem(EXPIRY_KEY);
    const refreshToken = localStorage.getItem(REFRESH_KEY);

    if (!stored || !expiry) {
      setLoading(false);
      return;
    }

    const expiresAt = new Date(expiry).getTime();
    const now = Date.now();

    if (expiresAt > now + 60_000) {
      // Token still valid
      setUser(JSON.parse(stored));
      setLoading(false);
    } else if (refreshToken) {
      // Expired — try a refresh once, silently
      api
        .refresh(refreshToken)
        .then((auth) => {
          storeSession(auth);
          setUser({ email: auth.email, plan: auth.plan });
        })
        .catch(() => clearSession())
        .finally(() => setLoading(false));
    } else {
      clearSession();
      setLoading(false);
    }
  }, []);
  /* eslint-enable react-hooks/set-state-in-effect */

  const login = useCallback(async (email: string, password: string) => {
    const auth = await api.login(email, password);
    storeSession(auth);
    setUser({ email: auth.email, plan: auth.plan });
  }, []);

  const register = useCallback(async (email: string, password: string) => {
    const auth = await api.register(email, password);
    storeSession(auth);
    setUser({ email: auth.email, plan: auth.plan });
    track("signup_completed", { method: "email" });
  }, []);

  const googleLogin = useCallback(async (idToken: string) => {
    const auth = await api.googleLogin(idToken);
    storeSession(auth);
    setUser({ email: auth.email, plan: auth.plan });
    track("signup_completed", { method: "google" });
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setUser(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, login, register, googleLogin, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
