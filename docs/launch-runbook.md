# RegimeDeck — Free Launch Runbook

Ordered, tickable steps for the **free** public launch (Pro stays dark:
`Features:ProEnabled` unset/false). Work top to bottom. Two items are load-bearing
and flagged 🔴 — don't skip their verification.

**Topology:** FE (Next.js) on Vercel at the apex `regimedeck.com`, API (ASP.NET Core,
.NET 10) + Azure SQL at `api.regimedeck.com`. Same registrable domain = clean cookies.
The **canonical FE origin is the apex** — `Frontend:Url`, `NEXT_PUBLIC_SITE_URL` and
Google's authorized origin must all be exactly `https://regimedeck.com`, no trailing slash.

---

## Phase 0 — Prep (no hosting yet)
- [x] **Domain purchased** — `regimedeck.com` (www). *(2026-07-23)*
- [x] **Canonical FE origin** — **apex: `https://regimedeck.com`** (www redirects to it).
      `Frontend:Url`, `NEXT_PUBLIC_SITE_URL` and Google's authorized JS origin all use
      exactly this, no trailing slash. *(2026-07-27)*
- [ ] **Fresh `Jwt:Secret`** — long random, store in the host secret manager (NOT git).
- [ ] **Dedicated Google OAuth app** — Cloud Console → Credentials → OAuth client ID
      (Web application). Authorized JS origin = the canonical FE origin. Copy the client ID.
- [ ] **Resend** — verify the sending domain (DNS records), grab the API key.
- [ ] **Legal mailboxes** — real `privacy@regimedeck.com` + `legal@regimedeck.com`
      (forwards OK). These exact addresses are printed on /privacy and /terms.
- [ ] **Named data controller** for /privacy + /terms (GDPR).
- [ ] Confirm `Features:ProEnabled` unset/false.

> **Status (2026-07-24):** Domain purchased; Resend domain verified + API key + From decided;
> Azure resource group `regimedeck-rg` + **Azure SQL** `RegimeDeck` (serverless GP, free offer set
> to *keep-running/bill-overage*) created, connection string in hand; subscription upgraded to
> Pay-As-You-Go. **Blocked:** App Service B1 create fails on a 0 "Total VMs" quota — App Service
> quota-increase request filed (East US, new limit 3). Resume: once quota clears, create the B1
> Web App (Linux, .NET 10), flip Always On, then deployment + env vars. Backend is now on **.NET 10**.
>
> **Status (2026-07-27):** Canonical origin decided = **apex**. Both repos pushed clean.
> Legal-page mailboxes corrected from the never-owned `regimedeck.app` to `regimedeck.com`.
> Still blocked on the same App Service quota request — nothing else in Phase 1+ can start
> until it clears (or we take the VPS fallback).
>
> **Status (2026-07-30) — UNBLOCKED.** The quota was never granted; the fix was to stop asking.
> East US had no capacity for a new subscription, and **creating the Web App in France worked
> immediately**. Lesson for any future Azure resource here: try another region before filing a
> quota ticket. **Regions:** App Service = **France Central** (Central US and East US 2 both
> refused too — France was the one that took). Azure SQL = **Germany West Central**, so the split
> is intra-EU (~10ms), not transatlantic — so the DB was recreated in France Central as
> **Standard S0** while it was still empty: same-region latency, no inter-region egress, and
> the move off serverless in one action. **Windows→Linux:** the first Web App was created on
> **Windows**, which is ~4x the price of Linux at B1 (~$55 vs $13.14/mo) for an app with zero
> Windows dependencies. Replaced with a Linux B1 before deploying. **Running Azure cost: ~$31/mo**
> (App Service $13.14 + SQL S0 $18.40), against the ~$190/mo the original serverless setup implied.

## Phase 1 — Provision
- [x] **Database** — Azure SQL `regime-deck-fc` / `RegimeDeck`, **France Central**, **Standard S0
      (DTU)**, locally-redundant backups. *Not serverless*: it bills per vCore-second whenever the
      DB is online, and the background jobs (densest cadence 4h) plus real traffic mean it never
      auto-pauses — the free grant is ~100k vCore-seconds (~55h at a 0.5 vCore floor, ~7% of a
      month), after which it runs ~$80-175/mo. S0 is a flat ~$18.40. *(2026-07-30)*
- [x] **API host with HTTPS** — Azure App Service `regime-deck-wa-linux`, **Linux B1, France
      Central, .NET 10**, Always On enabled. *(2026-07-30)*
- [x] **API custom domain** — `api.regimedeck.com` bound with a free App Service Managed
      Certificate (SNI, DigiCert, auto-renewing). DNS is Cloudflare: CNAME `api` → the app's
      default host, plus TXT `asuid.api` = the custom-domain verification ID. **Both must be
      "DNS only" (grey cloud)** — Cloudflare's proxy terminates TLS itself, which blocks both
      issuance and auto-renewal of the managed cert. *(2026-07-30)*
- [ ] **FE** — Vercel project linked to `swing-signal-web`.

## Phase 2 — Configure the API (prod env)
- [x] `ConnectionStrings__SqlServer` — note App Service on Linux needs `__`, not `:`
- [x] `Jwt__Secret` (fresh 64-byte base64), `Jwt:Issuer=RegimeDeck`, `Jwt:Audience=RegimeDeckWeb` (from appsettings)
- [x] `Google__ClientId` (dedicated app, JS origin `https://regimedeck.com`)
- [x] `Resend__ApiKey`, `Resend__From="RegimeDeck <noreply@regimedeck.com>"`, `Resend:DevRedirectTo` not set
- [x] `Fred__ApiKey`
- [x] `Frontend__Url=https://regimedeck.com` — exact, no trailing slash (drives CORS + CSRF Origin check)
- [x] `ForwardedHeaders__Enabled=true`
- [x] `ASPNETCORE_ENVIRONMENT=Production`, `Features__ProEnabled=false`
- [x] **Validated from a local machine before deploying** (2026-07-30): booted the API with
      `ASPNETCORE_ENVIRONMENT=Production` against the prod DB. Config fail-fast passed, all
      **12** migrations applied (InitialCreate → AddAlertRules), 11 assets seeded, and FRED /
      Binance / Yahoo / CoinMetrics ingestion all ran — so the DB is warm before first deploy.
      Worth repeating for any future environment change; it catches config errors without
      burning deploy cycles. (`Failed to determine the https port` is expected on local http.)

### Deploy gotchas hit on 2026-07-30 (all cost real time — read before touching the deploy)
- **The connection string belongs in *App settings*, not the *Connection strings* blade.** That
  blade prefixes entries as env vars (`SQLCONNSTR_<name>`), and .NET maps `SQLCONNSTR_X` →
  `ConnectionStrings:X`. Named `ConnectionStrings__SqlServer` there, it resolved to
  `ConnectionStrings:ConnectionStrings:SqlServer`, so prod config fail-fast killed startup.
  Either use an App setting named `ConnectionStrings__SqlServer` (what we did), or a
  Connection-strings entry named just `SqlServer`.
- **Publish the project, never the solution.** .NET 10 *allows* `dotnet publish -o` on a
  solution, so Azure's generated workflow silently published all six projects into `wwwroot` —
  test DLLs and a second `.runtimeconfig.json` included. Two runtimeconfigs = App Service can't
  identify the entry point, so it serves `hostingstart.html` and every route 404s.
- **Zip deploy merges; it does not replace.** Fixing the workflow wasn't enough — the earlier
  junk stayed. Had to empty `wwwroot` (Kudu `POST /api/command`) and redeploy. `az webapp deploy
  --clean` returned Kudu 400, and local `Compress-Archive` zips are rejected too (backslash
  entry paths) — re-running the GitHub workflow is the reliable route.
- **"Secure unique default hostname" is on by default**, so the host is
  `<app>-<hash>.<region>-01.azurewebsites.net`, not `<app>.azurewebsites.net`. Read it off
  Overview → Default domain.
- **A 500 right after first boot is probably not a bug.** `/api/regime/current` timed out at 30s
  while the initial backfill was running (index present, DTU idle — pure contention). It returns
  in ~1s once ingestion settles. Re-test before investigating.
- Diagnosis needs `az`; the portal can't show `wwwroot` contents or the container log. Useful:
  `az webapp config appsettings list`, and Kudu `api/command` for `ls`/`grep` over `/home/LogFiles`.

## Phase 3 — Configure the FE (Vercel env) — DONE 2026-07-30
- [x] `NEXT_PUBLIC_API_URL=https://api.regimedeck.com`
- [x] `NEXT_PUBLIC_SITE_URL=https://regimedeck.com`
- [x] `NEXT_PUBLIC_GOOGLE_CLIENT_ID=<dedicated>`
- [x] `NEXT_PUBLIC_PRO_ENABLED=false`
- [ ] `NEXT_PUBLIC_UMAMI_SRC` + `NEXT_PUBLIC_UMAMI_WEBSITE_ID` (optional, can follow)
- [x] **Deployed.** `https://regimedeck.com` live on a Let's Encrypt cert.
      DNS (Cloudflare, every record **DNS only / grey cloud**):
      apex `@` CNAME → `<hash>.vercel-dns-017.com` (Cloudflare flattens at the apex),
      `www` CNAME → same, `api` CNAME → the App Service host, `asuid.api` TXT → verification id.
- [x] **`www` must REDIRECT, not serve.** Vercel's add-domain dialog offers "redirect apex to
      www (recommended)" — leave it **unchecked**, because the apex is canonical here. But
      unchecking it adds `www` as a plain alias that serves the site on its own hostname, which
      is worse than either option: sign-in from `www` sends `Origin: https://www.regimedeck.com`,
      which fails CORS and the Origin CSRF check against `Frontend__Url` and looks like a random
      intermittent auth bug. Set `www` → Redirect → apex, 308. Verify with
      `curl -o /dev/null -w "%{http_code} %{redirect_url}" https://www.regimedeck.com/`.
- [x] **Verify the API base URL is in the client bundle**, not just that pages render. Server
      rendering works off build-time fetches, so a missing `NEXT_PUBLIC_API_URL` still produces a
      perfect-looking site while every client call silently falls back to `https://localhost:7260`
      (`lib/api.ts`). Grep the `/_next/static/chunks/*.js` for the real host.

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
