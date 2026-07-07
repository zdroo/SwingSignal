// Thin wrapper over Umami (or any script exposing window.umami.track).
// No-ops silently when analytics isn't configured — dev stays clean.

declare global {
  interface Window {
    umami?: { track: (event: string, data?: Record<string, unknown>) => void };
  }
}

export function track(event: string, data?: Record<string, unknown>) {
  if (typeof window === "undefined") return;
  try {
    window.umami?.track(event, data);
  } catch {
    // analytics must never break the app
  }
}
