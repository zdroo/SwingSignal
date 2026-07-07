import { ImageResponse } from "next/og";

export const size = { width: 1200, height: 630 };
export const contentType = "image/png";
export const alt = "SwingSignal — Honest Odds for Swing Traders";

export default function Image() {
  return new ImageResponse(
    (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          alignItems: "center",
          background: "#09090b",
          fontFamily: "sans-serif",
        }}
      >
        {/* Sparkline motif */}
        <svg width="220" height="110" viewBox="0 0 220 110">
          <path
            d="M10 90 L60 45 L95 70 L150 25 L170 25 L170 45"
            stroke="#10b981"
            strokeWidth="10"
            fill="none"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        <div style={{ display: "flex", fontSize: 84, fontWeight: 700, color: "#ffffff", marginTop: 12 }}>
          SwingSignal
        </div>
        <div style={{ display: "flex", fontSize: 34, color: "#a1a1aa", marginTop: 16 }}>
          What are the odds? Honest, backtested macro context.
        </div>
        <div
          style={{
            display: "flex",
            marginTop: 32,
            padding: "12px 28px",
            borderRadius: 12,
            border: "2px solid #10b981",
            color: "#10b981",
            fontSize: 26,
          }}
        >
          No signals. No promises. Verify our accuracy yourself.
        </div>
      </div>
    ),
    { ...size }
  );
}
