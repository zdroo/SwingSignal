import type {
  AssetDto,
  AssetOddsDto,
  AssetPeriodOddsDto,
  AuthResponse,
  BacktestComparisonDto,
  BacktestResultDto,
  HistoricalMatchDto,
  MacroRegimeDto,
  PopularAssetDto,
  SymbolSearchResultDto,
  UserProfileDto,
} from "@/types";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "https://localhost:7260";

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

// Client-side only — server components never have a token
function authHeaders(): Record<string, string> {
  if (typeof window === "undefined") return {};
  const token = localStorage.getItem("ss_access_token");
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    next: { revalidate: 300 },
    headers: authHeaders(),
  });
  if (!res.ok) throw new ApiError(res.status, `API error ${res.status} for ${path}`);
  return res.json();
}

async function send<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new ApiError(res.status, text || `API error ${res.status}`);
  }
  return res.json();
}

const post = <T,>(path: string, body: unknown): Promise<T> => send<T>("POST", path, body);
const put = <T,>(path: string, body: unknown): Promise<T> => send<T>("PUT", path, body);
const del = <T,>(path: string): Promise<T> => send<T>("DELETE", path);

export const api = {
  getCurrentRegime: (): Promise<MacroRegimeDto> =>
    get("/api/regime/current"),

  getHistoricalMatches: (topK = 10): Promise<HistoricalMatchDto[]> =>
    get(`/api/regime/matches?topK=${topK}`),

  getAssetOdds: (symbol: string, topK = 10, meta?: { q?: string; src?: string }): Promise<AssetOddsDto> => {
    const params = new URLSearchParams({ topK: String(topK) });
    if (meta?.q) params.set("q", meta.q);
    if (meta?.src) params.set("src", meta.src);
    return get(`/api/regime/odds/${encodeURIComponent(symbol)}?${params.toString()}`);
  },

  getOddsForPeriod: (symbol: string, days: number, topK = 10): Promise<AssetPeriodOddsDto> =>
    get(`/api/regime/odds/${encodeURIComponent(symbol)}/period?days=${days}&topK=${topK}`),

  getAssets: (): Promise<AssetDto[]> =>
    get("/api/assets"),

  searchSymbols: (q: string): Promise<SymbolSearchResultDto[]> =>
    get(`/api/assets/search?q=${encodeURIComponent(q)}`),

  getPopularAssets: (): Promise<PopularAssetDto[]> =>
    get("/api/assets/popular"),

  runBacktest: (symbol: string, days: number, topK = 10): Promise<BacktestResultDto> =>
    get(`/api/backtest/${encodeURIComponent(symbol)}?days=${days}&topK=${topK}`),

  compareBacktest: (symbol: string, days: number, topK = 10): Promise<BacktestComparisonDto> =>
    get(`/api/backtest/${encodeURIComponent(symbol)}/compare?days=${days}&topK=${topK}`),

  register: (email: string, password: string): Promise<AuthResponse> =>
    post("/api/auth/register", { email, password }),

  login: (email: string, password: string): Promise<AuthResponse> =>
    post("/api/auth/login", { email, password }),

  refresh: (refreshToken: string): Promise<AuthResponse> =>
    post("/api/auth/refresh", { refreshToken }),

  googleLogin: (idToken: string): Promise<AuthResponse> =>
    post("/api/auth/google", { idToken }),

  confirmEmail: (token: string): Promise<{ message: string }> =>
    post("/api/auth/confirm-email", { token }),

  resendConfirmation: (email: string): Promise<{ message: string }> =>
    post("/api/auth/resend-confirmation", { email }),

  forgotPassword: (email: string): Promise<{ message: string }> =>
    post("/api/auth/forgot-password", { email }),

  resetPassword: (token: string, newPassword: string): Promise<{ message: string }> =>
    post("/api/auth/reset-password", { token, newPassword }),

  joinWaitlist: (email: string, source: string): Promise<{ message: string }> =>
    post("/api/waitlist", { email, source }),

  getProfile: (): Promise<UserProfileDto> =>
    get("/api/users/me"),

  changePassword: (currentPassword: string, newPassword: string): Promise<{ message: string }> =>
    put("/api/users/me/password", { currentPassword, newPassword }),

  deleteAccount: (): Promise<{ message: string }> =>
    del("/api/users/me"),
};
