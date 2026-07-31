# How RegimeDeck Was Deployed

The end-to-end account of getting RegimeDeck from a purchased domain to a live
site, written so it could be repeated on a second environment. `launch-runbook.md`
is the *status* checklist; this is the *procedure*, including the things that went
wrong, because most of the elapsed time went into those rather than the happy path.

**Final topology**

| Piece | Where | Notes |
|---|---|---|
| Frontend | Vercel, `regimedeck.com` | Next.js 16, Let's Encrypt cert |
| API | Azure App Service, `api.regimedeck.com` | Linux **B1**, France Central, .NET 10, Always On |
| Database | Azure SQL `RegimeDeck` on `regime-deck-fc` | **Standard S0 (DTU)**, France Central |
| DNS | Cloudflare | every record **DNS only / grey cloud** |
| CI/CD | GitHub Actions | OIDC to Azure, no publish profile |

Running cost ≈ **$31/mo** (App Service $13.14 + SQL $18.40).

---

## 1. Domain

`regimedeck.com` registered, nameservers pointed at Cloudflare.

**The one decision that everything else keys off: apex vs www.** We chose the
**apex**, `https://regimedeck.com`. That exact string — no trailing slash, `https`,
no `www` — has to appear identically in four places:

- `Frontend__Url` (API app setting) — drives CORS **and** the Origin CSRF check
- `NEXT_PUBLIC_SITE_URL` (Vercel) — canonicals, sitemap, OG tags
- Google OAuth **Authorized JavaScript origins**
- Vercel's primary domain

Decide this before provisioning anything. Changing it later means editing all four
and rebuilding the frontend.

---

## 2. Azure — database

Created via Portal → **SQL Database**.

| Setting | Value | Why |
|---|---|---|
| Resource group | `regimedeck-rg` | |
| Server | `regime-deck-fc`, **France Central** | must match the App Service region |
| Auth | SQL authentication | the app uses a connection string |
| Purchasing model | **DTU → Standard → S0** | see below |
| Backup redundancy | **Locally-redundant** | data is re-ingestible; geo-redundant costs ~2× |
| Networking | Public endpoint, **Allow Azure services = Yes**, add client IP | |
| Collation | `SQL_Latin1_General_CP1_CI_AS` (default) | matches local dev; unchangeable later |

### ⚠ Do not use the serverless "free offer" for this workload

The free offer grants **100,000 vCore-seconds/month** — about **55 hours** online at
the 0.5 vCore floor, roughly 7% of a month. Auto-pause needs 60 minutes with zero
connections, but RegimeDeck runs **11 background services** whose densest cadence is
4 hours (~27 wake-ups/day, average gap ~53 min). It therefore *never* pauses, and:

- set to "pause when exhausted" → the database goes offline ~27 days of every month
- set to "bill overage" → roughly **$80–175/mo**

Fixed-price **S0 at ~$18.40** is both cheaper and predictable. The blade defaults to
vCore/serverless, and it silently reverted to **Hyperscale, 2 vCores ($333/mo)** once
during setup — always re-read the price on the confirmation screen before creating.
Hyperscale is close to one-way; you cannot scale back to Standard without export/import.

### The free-offer region trap

Creating a second free-offer database fails with *"All free databases must be in the
same region."* The fix is **not** to delete anything — it's to **untick the free-offer
checkbox** on the Basics tab. The region restriction only applies to free-offer databases.

---

## 3. Azure — App Service

### Quota: change region, don't file a ticket

`Total VMs = 0` on a newly upgraded Pay-As-You-Go subscription blocked B1 creation in
**East US**. A quota-increase ticket sat unanswered for six days. **Central US** and
**East US 2** also refused. **France Central worked immediately.**

Lesson: on a new subscription, a zero VM quota is usually regional capacity, not an
account limit. Try other regions first — it takes two minutes versus days of waiting.

### Linux, not Windows

The first Web App was created on **Windows**, which is ~4× the price of Linux at B1
(~$55 vs **$13.14**/mo) for an app with zero Windows dependencies. The OS is fixed at
App Service *plan* creation, so this meant a new plan **and** a new web app.

Create the **Web App** (not the plan separately) — the plan is created underneath:

- Publish: **Code** · Runtime: **.NET 10 (LTS)** · OS: **Linux** · Region: **France Central**
- Pricing: **Basic B1** · then **Configuration → General settings → Always On: On**

**Always On is mandatory here.** Without it the platform unloads the app after ~20
minutes idle and all 11 background services stop with it.

### "Secure unique default hostname"

On by default, and it changes the hostname to
`<app>-<hash>.<region>-01.azurewebsites.net` rather than `<app>.azurewebsites.net`.
Read the real value from **Overview → Default domain** — it is not guessable.

### App settings

**Environment variables → App settings**, using `__` (double underscore), never `:`.

```
ConnectionStrings__SqlServer   <full ADO.NET string, real password>
Jwt__Secret                    <fresh 64 random bytes, base64>
Google__ClientId               <dedicated OAuth client id>
Resend__ApiKey                 <key>
Resend__From                   RegimeDeck <noreply@regimedeck.com>
Fred__ApiKey                   <key>
Frontend__Url                  https://regimedeck.com
ForwardedHeaders__Enabled      true
ASPNETCORE_ENVIRONMENT         Production
Features__ProEnabled           false
```

`Resend__DevRedirectTo` must **not** be set in production.

### ⚠ The connection string goes in App settings, NOT the "Connection strings" blade

This cost an hour. That blade prefixes entries as environment variables
(`SQLCONNSTR_<name>`), and .NET's configuration then maps `SQLCONNSTR_X` →
`ConnectionStrings:X`. An entry named `ConnectionStrings__SqlServer` therefore resolves
to **`ConnectionStrings:ConnectionStrings:SqlServer`**, which doesn't exist — so the
production fail-fast validator in `Program.cs` killed startup, and App Service served
its placeholder page with every route 404ing.

Either use an **App setting** named `ConnectionStrings__SqlServer` (what we did), or a
Connection-strings entry named just **`SqlServer`**.

---

## 4. Validate production config *before* deploying

Worth repeating for any new environment — it catches configuration errors without
burning deploy cycles, and warms the database so the first deploy starts with data.

```powershell
$env:ASPNETCORE_ENVIRONMENT       = 'Production'
$env:ConnectionStrings__SqlServer = '<prod connection string>'
$env:Jwt__Secret                  = '<secret>'
$env:Google__ClientId             = '<client id>'
$env:Resend__ApiKey               = '<key>'
$env:Fred__ApiKey                 = '<key>'
$env:Frontend__Url                = 'https://regimedeck.com'
dotnet run --project RegimeDeck.Api --no-launch-profile
```

Watch for, in order: no *"Missing required production configuration"*; EF applying all
migrations; asset seeding; ingestion logging. This run applied 12 migrations, seeded 11
assets and ingested ~74k candles and ~144k macro points into the production database
before anything was deployed.

`Failed to determine the https port for redirect` is expected over local http.

---

## 5. Linking GitHub to Azure

**App Service → Deployment Center → GitHub**, then:

- Organization / Repository / Branch — here `zdroo` / `SwingSignal` / **`master`**
- Workflow option: **Add a workflow**
- Authentication: **User-assigned managed identity** ← OIDC, not a publish profile

Azure creates the managed identity, federates it with GitHub, adds three repository
secrets (`AZUREAPPSERVICE_CLIENTID_…`, `TENANTID`, `SUBSCRIPTIONID`) and commits
`.github/workflows/master_regime-deck-wa-linux.yml`.

This is why **basic authentication can stay disabled** on the web app — nothing uses a
publish profile. It is both the easier and the more secure route.

### ⚠ Azure's generated workflow needs two fixes

As generated it runs:

```yaml
- run: dotnet build --configuration Release
- run: dotnet publish -c Release -o ${{env.DOTNET_ROOT}}/myapp
```

Neither names a project, so both resolve `RegimeDeck.slnx` — **six** projects including
`RegimeDeck.Tests`. The .NET 10 SDK *permits* publishing a solution to a single output
folder, so this **succeeds** and quietly deploys test assemblies (`coverlet.collector`,
`Mvc.Testing`, `TestHost`) plus a **second `.runtimeconfig.json`**. Two runtimeconfigs
mean App Service cannot identify the entry point, so it runs its default handler and
serves `hostingstart.html` — a green build and a dead site.

It also doesn't run the tests, so a red build would still ship.

Corrected:

```yaml
# Whole solution: Directory.Build.props enforces TreatWarningsAsErrors.
- name: Build
  run: dotnet build RegimeDeck.slnx --configuration Release

# ci.yml runs in parallel and can't hold back a deploy — gate here too.
- name: Test
  run: dotnet test RegimeDeck.Tests/RegimeDeck.Tests.csproj --configuration Release --no-build

# Name the project, or the test project ships with it.
- name: Publish
  run: dotnet publish RegimeDeck.Api/RegimeDeck.Api.csproj -c Release -o publish
```

with the artifact path changed to `publish`.

### ⚠ Zip deploy merges; it does not replace

Fixing the workflow was not enough — the earlier six-project dump stayed in `wwwroot`
and kept breaking startup. It had to be emptied explicitly via the Kudu command API:

```powershell
# POST https://<app>.scm.<region>-01.azurewebsites.net/api/command
# body: {"command":"find /home/site/wwwroot -mindepth 1 -delete","dir":"/home/site"}
az rest --method post --uri "https://$scm/api/command" `
  --resource "https://management.core.windows.net/" `
  --headers "Content-Type=application/json" --body "@clean.json"
```

Then re-run the workflow (`gh workflow run master_regime-deck-wa-linux.yml`).

Two local alternatives that **do not** work: `az webapp deploy --clean` returns Kudu
400, and zips built by PowerShell's `Compress-Archive` are rejected because they use
backslash entry paths. Let the Linux runner build the package.

### The other CI workflow

`ci.yml` (build + 393 tests) had been triggering on `branches: [main]` while the repo
uses **`master`** — so it had **never run once** since the repo split. The frontend's
equivalent *was* running and had been **failing on every push for five days**, unnoticed.

Check that CI has actually executed, not merely that nobody reported a failure.

---

## 6. Custom domain + TLS for the API

Order matters: DNS first, then binding, then certificate.

**Cloudflare records** (both **DNS only / grey cloud**):

| Type | Name | Value |
|---|---|---|
| CNAME | `api` | `<app>-<hash>.<region>-01.azurewebsites.net` |
| TXT | `asuid.api` | the Custom Domain Verification ID |

Get the verification id with:
```powershell
az webapp show -g regimedeck-rg -n regime-deck-wa-linux --query customDomainVerificationId -o tsv
```

Then bind and issue the free managed certificate:

```powershell
az webapp config hostname add --webapp-name regime-deck-wa-linux -g regimedeck-rg --hostname api.regimedeck.com
az webapp config ssl create   -g regimedeck-rg --name regime-deck-wa-linux --hostname api.regimedeck.com
az webapp config ssl bind     -g regimedeck-rg --name regime-deck-wa-linux --certificate-thumbprint <tb> --ssl-type SNI
```

`ssl create` returns a JSON deserialization traceback — a cosmetic `az` bug; the
operation runs. Poll with `az webapp config ssl show --certificate-name api.regimedeck.com`.

**The Cloudflare proxy must stay off.** An orange-clouded record makes Cloudflare
terminate TLS itself, which blocks both issuance *and* the automatic renewal months
later — long after anyone remembers why.

---

## 7. Vercel

1. **Import** `zdroo/swing-signal-web` — Next.js auto-detected, build settings untouched.
2. **Environment variables**, set **before** the first build (`NEXT_PUBLIC_*` values are
   inlined at build time; adding them later requires a rebuild):

   ```
   NEXT_PUBLIC_API_URL          https://api.regimedeck.com
   NEXT_PUBLIC_SITE_URL         https://regimedeck.com
   NEXT_PUBLIC_GOOGLE_CLIENT_ID <same id as Google__ClientId>
   NEXT_PUBLIC_PRO_ENABLED      false
   ```
3. **Domains** → add `regimedeck.com` and `www.regimedeck.com`, both **Production**.

### ⚠ The first build failed — and CI had been saying so for days

```
useSearchParams() should be wrapped in a suspense boundary at page "/account"
Error occurred prerendering page "/account"
```

`useSearchParams()` opts its component tree into client-side rendering, and a
prerendered route needs a `<Suspense>` boundary above it. **`next dev` never
prerenders, so the page worked perfectly in development** — only `next build` catches
it. Fixed by moving the page body into `AccountPageContent` and wrapping it, reusing
the spinner the page already showed while auth resolved.

### ⚠ "Redirect apex domains to www (recommended)" — leave it UNCHECKED

It is Vercel's general default, but wrong when the apex is canonical: it would make
`www` the serving origin, breaking `Frontend__Url`, the baked-in `NEXT_PUBLIC_SITE_URL`
and the Google origin all at once.

**But unchecking it is not sufficient.** `www` then gets added as a plain alias that
*serves* the site on its own hostname — arguably worse than either option, because
sign-in from `www` sends `Origin: https://www.regimedeck.com`, which fails the API's
CORS and Origin CSRF checks and presents as a random intermittent auth bug.

Explicitly set `www.regimedeck.com` → **Redirect to `regimedeck.com`, 308**. Verify:

```bash
curl -s -o /dev/null -w "%{http_code} -> %{redirect_url}\n" https://www.regimedeck.com/dashboard
# 308 -> https://regimedeck.com/dashboard
```

### DNS for the frontend

| Type | Name | Value | Proxy |
|---|---|---|---|
| CNAME | `@` | `<hash>.vercel-dns-017.com` | **DNS only** |
| CNAME | `www` | same | **DNS only** |

A CNAME at the zone apex is illegal in plain DNS; Cloudflare flattens it automatically.
Preferred over the legacy `76.76.21.21` A record, which pins you to one IP.

Vercel shows *"Invalid Configuration"* until the records resolve, and issues each
certificate separately — the apex came up about a minute before `www`.

### ⚠ Verify the API URL reached the client bundle

Pages rendering correctly proves nothing: server-side rendering uses build-time fetches,
so a missing `NEXT_PUBLIC_API_URL` yields a perfect-looking site while every *client*
call silently falls back to `https://localhost:7260` (the default in `lib/api.ts`) and
all auth breaks.

```bash
curl -s https://regimedeck.com/auth | grep -oE '/_next/static/chunks/[^"]+\.js' | sort -u |
  while read c; do curl -s "https://regimedeck.com$c" | grep -l "api.regimedeck.com" - ; done
```

---

## 8. Google OAuth

A **dedicated** client — development had been borrowing another project's ID.

Cloud Console → **APIs & Services → Credentials → Create credentials → OAuth client ID
→ Web application**:

- **Authorized JavaScript origins:** `https://regimedeck.com`, plus `http://localhost:3000` for dev
- **Authorized redirect URIs:** *empty* — `@react-oauth/google` is a JS popup flow, not
  a server redirect, so entries here do nothing
- No `www` entry needed once `www` 308-redirects

The origin must match **exactly**: `https://regimedeck.com/` (trailing slash) is a
different origin to Google and is rejected. The same client ID must appear in the OAuth
app, `Google__ClientId` (App Service) and `NEXT_PUBLIC_GOOGLE_CLIENT_ID` (Vercel).
Changes can take a few minutes to propagate.

---

## 9. Verification

### Auth cookie flow — the load-bearing check

Server side, repeatable after any origin or CORS change:

```bash
# preflight from the real origin → 204 + Allow-Credentials + Allow-Origin
curl -i -X OPTIONS https://api.regimedeck.com/api/auth/refresh \
  -H "Origin: https://regimedeck.com" -H "Access-Control-Request-Method: POST"

curl -X POST https://api.regimedeck.com/api/auth/refresh -H "Origin: https://regimedeck.com"      # 401 (no cookie)
curl -X POST https://api.regimedeck.com/api/auth/refresh -H "Origin: https://evil.example.com"    # 403
curl -X POST https://api.regimedeck.com/api/auth/refresh -H "Origin: https://www.regimedeck.com"  # 403
```

In a real browser on the apex: sign in, **hard reload** (must stay signed in, with
`POST /api/auth/refresh` → 200), then log out (next refresh → 401). The cookie should be
`HttpOnly` + `Secure` + `SameSite=None` + `Path=/api/auth`.

Diagnosing a reload that logs users out: **401** = cookie not sent (`SameSite`/`Secure`
/ forwarded headers), **403** = Origin mismatch against `Frontend__Url`.

### A 500 right after first boot is probably not a bug

`/api/regime/current` timed out at 30s during the initial backfill — index present, DTU
idle, pure contention. It returned in ~1s once ingestion settled. Re-test before
investigating. Likewise `/api/screener` is empty until `ScreenerComputeService` clears
its 10-minute startup delay.

---

## 10. Diagnosing App Service without the Portal

The Portal cannot show `wwwroot` contents or the container log. These were decisive:

```powershell
az webapp config show           -g <rg> -n <app> --query "{cmd:appCommandLine,alwaysOn:alwaysOn,fx:linuxFxVersion}"
az webapp config appsettings list -g <rg> -n <app> --query "[].name"        # names only — no secrets
az webapp config connection-string list -g <rg> -n <app> --query "[].name"
az webapp log deployment list   -g <rg> -n <app>                            # status 4 = success

# Kudu shell — list wwwroot, grep the container log
az rest --method post --uri "https://<scm-host>/api/command" \
        --resource "https://management.core.windows.net/" \
        --headers "Content-Type=application/json" --body '@cmd.json'
```

The unhandled-exception stack traces live in `/home/LogFiles/StartupLogs/*_success.log`,
not the `*_docker.log` (which carries platform messages). Enable app logging first:

```powershell
az webapp log config -g <rg> -n <app> --application-logging filesystem --level information --docker-container-logging filesystem
```

---

## 11. Rotating the SQL admin password

Server first, then the app setting, so the window where they disagree is seconds:

```powershell
az sql server update -g regimedeck-rg -n regime-deck-fc --admin-password $new
az webapp config appsettings set -g regimedeck-rg -n regime-deck-wa-linux --settings "@setting.json"
```

Pass the setting via a JSON file — a connection string contains many `=` characters and
CLI `name=value` splitting is unreliable. Generate the password from an alphabet that
excludes `;` `'` `"` `=` `{` `}`, which otherwise corrupt connection strings or shell
quoting.

---

## Summary of what actually went wrong

Nine issues, of which **six presented as the same symptom** — a running site serving a
placeholder page:

1. App Service quota 0 in East US → hop region
2. Web App created on Windows → 4× cost → recreate on Linux
3. Connection string in the wrong blade → double-prefixed key → startup killed
4. Workflow published the solution → test DLLs in `wwwroot` → ambiguous entry point
5. Zip deploy merges → junk persisted → wipe `wwwroot` manually
6. Backend CI on the wrong branch → never ran
7. Frontend CI red for five days → unnoticed → blocked the first Vercel build
8. `useSearchParams` without Suspense → only `next build` catches it
9. `www` added as an alias rather than a redirect → 403 on sign-in for www visitors

The recurring theme: **green does not mean working.** Both deploy runs went green while
the site was dead; the Vercel build looked fine while CI had been failing for days. Verify
the outcome — an endpoint returning real data — not the pipeline's own status.
