import Script from "next/script";

// Privacy-friendly analytics (Umami). Loads only when configured, so dev
// and self-hosters without analytics are unaffected. No cookies, GDPR-safe.
const SRC = process.env.NEXT_PUBLIC_UMAMI_SRC;
const WEBSITE_ID = process.env.NEXT_PUBLIC_UMAMI_WEBSITE_ID;

export function AnalyticsScript() {
  if (!SRC || !WEBSITE_ID) return null;
  return <Script src={SRC} data-website-id={WEBSITE_ID} strategy="afterInteractive" />;
}
