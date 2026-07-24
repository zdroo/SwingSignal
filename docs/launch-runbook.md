# RegimeDeck — Free Launch Runbook

Ordered, tickable steps for the **free** public launch (Pro stays dark:
`Features:ProEnabled` unset/false). Work top to bottom. Two items are load-bearing
and flagged 🔴 — don't skip their verification.

**Topology:** FE (Next.js) on Vercel at `regimedeck.com`, API (ASP.NET Core 8) +
SQL Server at `api.regimedeck.com`. Same registrable domain = clean cookies.
Decide the **canonical FE origin** (apex `regimedeck.com` vs `www.regimedeck.com`)
early — `Frontend:Url` and Google's authorized origin must match it **exactly**.

---

## Phase 0 — Prep (no hosting yet)
- [x] **Domain purchased** — `regimedeck.com` (www). *(2026-07-23)*
- [ ] **Decide canonical FE origin** — apex vs www. Everything below keys off it.
- [ ] **Fresh `Jwt:Secret`** — long random, store in the host secret manager (NOT git).
- [ ] **Dedicated Google OAuth app** — Cloud Console → Credentials → OAuth client ID
      (Web application). Authorized JS origin = the canonical FE origin. Copy the client ID.
- [ ] **Resend** — verify the sending domain (DNS records), grab the API key.
- [ ] **Legal mailboxes** — real `privacy@` + `legal@` (forwards OK).
- [ ] **Named data controller** for /privacy + /terms (GDPR).
- [ ] Confirm `Features:ProEnabled` unset/false.

> **Status (2026-07-24):** Domain purchased; Resend domain verified + API key + From decided;
> Azure resource group `regimedeck-rg` + **Azure SQL** `RegimeDeck` (serverless GP, free offer set
> to *keep-running/bill-overage*) created, connection string in hand; subscription upgraded to
> Pay-As-You-Go. **Blocked:** App Service B1 create fails on a 0 "Total VMs" quota — App Service
> quota-increase request filed (East US, new limit 3). Resume: once quota clears, create the B1
> Web App (Linux, .NET 10), flip Always On, then deployment + env vars. Backend is now on **.NET 10**.

## Phase 1 — Provision
- [ ] **Database** — Azure SQL (easiest) or SQL Server on a VPS. Get connection string.
- [ ] **API host with HTTPS** — Azure App Service (Linux, .NET 8) simplest; VPS needs
      Caddy/nginx + certbot. HTTPS is mandatory (the refresh cookie is `Secure`).
- [ ] **FE** — Vercel project linked to `swing-signal-web`.

## Phase 2 — Configure the API (prod env)
- [ ] `ConnectionStrings:SqlServer`
- [ ] `Jwt:Secret` (fresh), `Jwt:Issuer=RegimeDeck`, `Jwt:Audience=RegimeDeckWeb`
- [ ] `Google:ClientId` (dedicated)
- [ ] `Resend:ApiKey`, `Resend:From="RegimeDeck <you@regimedeck.com>"`, **remove** `Resend:DevRedirectTo`
- [ ] `Fred:ApiKey`
- [ ] `Frontend:Url=https://<canonical-fe-origin>` — exact, no trailing slash (drives CORS + CSRF Origin check)
- [ ] `ForwardedHeaders:Enabled=true`
- [ ] `ASPNETCORE_ENVIRONMENT=Production`, `Features:ProEnabled=false`
- [ ] First boot auto-migrates + seeds; wait ~30 min for boards to populate. Allow outbound (FRED/Yahoo/Binance).

## Phase 3 — Configure the FE (Vercel env)
- [ ] `NEXT_PUBLIC_API_URL=https://api.regimedeck.com`
- [ ] `NEXT_PUBLIC_SITE_URL=https://<canonical-fe-origin>`
- [ ] `NEXT_PUBLIC_GOOGLE_CLIENT_ID=<dedicated>`
- [ ] `NEXT_PUBLIC_PRO_ENABLED=false`
- [ ] `NEXT_PUBLIC_UMAMI_SRC` + `NEXT_PUBLIC_UMAMI_WEBSITE_ID` (optional, can follow)
- [ ] Deploy. DNS: apex/www → Vercel, `api` → API host.

## Phase 4 — 🔴 Auth cookie smoke test (the new flow — verify in the real browser)
- [ ] Register → `Set-Cookie: rd_refresh=…; Secure; HttpOnly; SameSite=None; Path=/api/auth`, land logged in.
- [ ] Hard reload → still logged in (`POST /api/auth/refresh` → 200).
- [ ] Logout → next refresh is 401.
- [ ] If reload logs you out: `Frontend:Url` mismatch, or cookie not `Secure`/`SameSite=None` (HTTPS/forwarded-headers not wired).

## Phase 5 — Smoke tests
- [ ] `/health` 200 · Google sign-in · email round-trip (real inbox) · odds page + backtest + screener + sectors render with data · OG preview · robots/sitemap. Submit sitemap to Search Console.

## Phase 6 — First 24h, then launch
- [ ] FRED ingestion filled all 26 indicators · phone pass over main pages · Umami counting.
- [ ] Launch posts (r/swingtrading, fintwit, Discords). Watch visitor→account, waitlist, weekly return.

---
**Deferred (NOT a free-launch blocker):** before flipping Pro on (billing), replace
the CoinMetrics CC BY-NC data source (non-commercial licensed).
