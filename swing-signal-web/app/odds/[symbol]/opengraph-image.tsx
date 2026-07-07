import { ImageResponse } from "next/og";

export const size = { width: 1200, height: 630 };
export const contentType = "image/png";
export const alt = "Historical odds and macro context";

export default async function Image({ params }: { params: Promise<{ symbol: string }> }) {
  const { symbol } = await params;
  const decoded = decodeURIComponent(symbol).toUpperCase().slice(0, 12);

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
        <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
          <svg width="64" height="64" viewBox="0 0 64 64">
            <path
              d="M8 48 L24 32 L34 42 L56 16"
              stroke="#10b981"
              strokeWidth="7"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <div style={{ display: "flex", fontSize: 36, fontWeight: 700, color: "#71717a" }}>
            SwingSignal
          </div>
        </div>

        <div style={{ display: "flex", fontSize: 110, fontWeight: 800, color: "#ffffff", marginTop: 12 }}>
          {decoded}
        </div>

        <div style={{ display: "flex", fontSize: 36, color: "#a1a1aa", marginTop: 12 }}>
          Historical odds &amp; macro context
        </div>

        <div
          style={{
            display: "flex",
            marginTop: 36,
            padding: "12px 28px",
            borderRadius: 12,
            border: "2px solid #10b981",
            color: "#10b981",
            fontSize: 26,
          }}
        >
          Based on 30+ years of macro regimes — backtest included
        </div>
      </div>
    ),
    { ...size }
  );
}
