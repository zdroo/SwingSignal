# Deploying RegimeDeck — the reasoning, not just the steps

How RegimeDeck went from a purchased domain to a live site, written so the *decisions*
transfer to the next project. `launch-runbook.md` is the status checklist; this explains
what each piece is, why it exists, and why we did things in the order we did.

**Where it ended up**

| Piece | Where | What it is |
|---|---|---|
| Frontend | Vercel, `regimedeck.com` | Next.js — build pipeline + CDN + serverless renderer |
| API | Azure App Service, `api.regimedeck.com` | Linux B1, France Central, .NET 10, Always On |
| Database | Azure SQL, `regime-deck-fc` | Standard S0 (DTU), France Central |
| DNS | Cloudflare | authoritative nameservers for the zone |
| CI/CD | GitHub Actions | OIDC into Azure, no stored credentials |

≈ **$31/mo** (App Service $13.14 + SQL $18.40).

---

# Part I — The mental model

Four independent systems had to be introduced to each other. Most deployment pain comes
from not knowing which one owns what.

**Azure App Service** runs your server process. **Azure SQL** stores your data.
**Vercel** builds and serves your frontend. **Cloudflare** answers the question "what IP
is `regimedeck.com`?" for the entire internet. **GitHub** holds the code and is the
trigger that moves new code into the first two.

None of them know about each other by default. Deployment is almost entirely the work of
creating references between them — a connection string, a DNS record, a trust
relationship, an allowed origin — and every one of those references is a string that has
to match exactly on both sides. Nearly every failure in this deployment was a mismatched
or misplaced reference, not broken code.

## Why the order was: database → compute → CI → domain → frontend → OAuth

The order is forced by a **dependency graph**, where each step needs an identifier that
only the previous step can produce:

```
Database ──(connection string)──► App Service ──(app's hostname)──► DNS record
                                       │                                │
                                       │                          (cert issuance)
                                       ▼                                ▼
                                 GitHub OIDC trust              api.regimedeck.com
                                                                        │
                                                          (NEXT_PUBLIC_API_URL)
                                                                        ▼
                                                                     Vercel
                                                                        │
                                                             (the final origin)
                                                                        ▼
                                                                 Google OAuth
```

Concretely:

**The database comes first because the API cannot start without it.** `Program.cs`
fail-fasts in Production when `ConnectionStrings:SqlServer` is missing, and on first boot
the app runs `db.Database.Migrate()` before serving anything. The database is a hard
dependency of the process — so it has to exist, and its connection string has to be in
hand, before the compute that consumes it is worth creating. Nothing about the database
depends on the API, so the arrow only points one way.

**Compute comes second because it produces the hostname everything downstream needs.**
You cannot write a DNS record for the API until Azure has told you what to point at.

**CI/CD is wired next because it's how code gets onto that compute**, and because it's far
easier to debug a deploy pipeline against a default `*.azurewebsites.net` hostname than to
debug it and a custom domain simultaneously. Keep the number of things that could be
wrong small.

**The custom domain and its certificate come after that**, because certificate issuance
validates against live DNS — the domain has to resolve to the app before a certificate
authority will vouch for it. That ordering is not a preference; it is mechanically
required.

**The frontend comes next because its build bakes in the API's address.** `NEXT_PUBLIC_*`
values are compiled into the JavaScript bundle at build time, so `api.regimedeck.com` must
be final before Vercel builds, or you rebuild.

**OAuth comes last because it needs the final, canonical frontend origin** — Google
validates the origin the browser reports, so that string can't be settled until the
frontend's domain is.

## Where we deviated, and what it cost

We created the database *before* knowing where compute could actually be provisioned. The
App Service quota then forced us into France Central while the database sat in Germany
West Central — so it had to be recreated.

The general rule this teaches: **establish where your compute can live before placing
anything it depends on.** Compute is the constrained resource (quotas, SKU availability,
region capacity); databases will happily be created almost anywhere. Provision the scarce
thing first, then colocate around it.

It only cost minutes because the database was still empty. Which is itself the lesson —
**the cheapest moment to move infrastructure is before it holds data**, so any doubt about
placement should be resolved immediately rather than after go-live.

---

# Part II — Azure SQL

## Why a managed database rather than one on the server

We could have run SQL Server in a container next to the API. A managed database instead
means backups, patching, TLS, and high availability are somebody else's job — you're
buying operational time, which for a solo pre-revenue project is the scarcest resource.
The trade is cost and a small amount of control.

## Why colocation matters more than it looks

The API and database ended up in the same region, and that is worth being deliberate
about. Every EF Core query is a network round trip. In-region is roughly **1 ms**;
across Europe roughly **10 ms**; across the Atlantic roughly **100 ms**.

That difference is invisible for a request-driven app doing two queries. RegimeDeck's
screener and sector-rotation jobs run query loops, so a per-query latency of 100 ms
turns seconds of work into minutes, on every cycle, forever. Colocation isn't
micro-optimisation here — it's the difference between a background job finishing and one
that never catches up.

Placing compute inside Azure also collapses the database firewall problem. Azure SQL's
firewall is an IP allowlist; hosts outside Azure have rotating egress IPs, so you'd end
up opening the database to `0.0.0.0/0` with only the password as a boundary. With the API
in Azure, the **"Allow Azure services"** toggle replaces IP management entirely and the
database is never publicly reachable.

## Why Standard S0 and not the free serverless tier

This is the decision with the largest cost swing, and it's arithmetic rather than taste.

**Serverless** bills per *vCore-second whenever the database is online*, and pauses after
a configurable idle period (minimum 60 minutes with zero connections). Its free grant is
**100,000 vCore-seconds/month** — at the 0.5 vCore floor, about **55 hours**, roughly 7%
of a month.

That model pays off only for databases that are genuinely idle most of the time. Ours
never is: **11 background services**, densest cadence 4 hours, roughly 27 wake-ups a day,
average gap ~53 minutes — below the 60-minute pause threshold before a single visitor
arrives. So the database is online ~730 hours a month, about **13× the grant**, and:

- *"pause when exhausted"* → the database goes offline ~27 days of every month
- *"bill overage"* → roughly **$80–175/mo**

**DTU-based Standard S0** is a flat ~$18.40 for continuously-available capacity. Paying
for steady capacity you continuously use is exactly what a fixed tier is for; serverless
charges a premium rate for elasticity you can't exploit.

> **On DTUs.** A DTU bundles CPU, memory and I/O into one number, so you turn a single
> dial instead of sizing three things. It's simpler but opaque — you can't tell whether
> you're CPU-bound or I/O-bound, or tune one axis. That's what the vCore model is for.
> For a small predictable workload, DTU is cheaper and the opacity doesn't cost you
> anything. Note that memory is *not* selectable in the DTU model; the storage slider is
> data size, not RAM.

**Watch the blade.** It defaults to vCore/serverless and once silently reverted to
**Hyperscale, 2 vCores — $333/mo**. Always re-read the price on the confirmation screen.
Hyperscale is effectively one-way; you can't scale back to Standard without export/import.

Related trap: creating a second free-offer database fails with *"All free databases must
be in the same region."* The fix isn't to delete anything — **untick the free-offer
checkbox**. The restriction only applies to free-offer databases.

## The other settings, and why

- **Locally-redundant backups** — geo-redundant costs ~2× to protect data that is almost
  entirely re-ingestible from FRED, Yahoo and Binance. A regional loss means re-running
  ingestion, not losing anything irreplaceable.
- **Collation `SQL_Latin1_General_CP1_CI_AS`** — matches local dev, so dev and prod behave
  identically, and it's case-insensitive, which the email lookups rely on. It cannot be
  changed after creation.
- **Connection policy: Default** — in-Azure clients get Redirect (straight to the node,
  lower latency), external clients get Proxy through the gateway. Forcing Redirect would
  require ports 11000–11999 outbound, which home and office networks usually block.
- **TLS 1.2 minimum**, `Encrypt=True;TrustServerCertificate=False` — `True` on that last
  one disables certificate validation entirely, discarding TLS's protection against a
  man-in-the-middle. Local dev sets it because localhost uses a self-signed certificate;
  that reasoning does not carry to a public endpoint.

---

# Part III — Azure App Service

## What it actually is

App Service is a **managed process host** — Platform-as-a-Service. You hand it a compiled
application; it provides the VM, the OS, the runtime, a public HTTPS endpoint, TLS
termination, a load balancer, log collection and a deployment mechanism. You never touch
the machine.

The layer beneath is worth understanding, because it's where cost and constraints live:

**App Service Plan** = the compute you rent — a VM (or several) of a given size, in a
given region, running a given OS. **This is the thing you pay for.**

**Web App** = an application running on that plan — a hostname, app settings, a
deployment. Several web apps can share one plan, competing for its resources.

Consequences that bit us:

- **OS and region are fixed at the plan level.** Discovering the plan was Windows meant
  creating a *new plan and a new web app* — you cannot flip an existing one.
- **Cost is per plan, not per app.** Windows B1 is ~4× Linux B1 (~$55 vs **$13.14**) for
  the same specs, because the price includes a Windows Server licence. For an app with no
  Windows dependencies, that's pure waste.
- **Scaling is a plan operation.** "Scale up" changes the SKU for everything on it.

## Why Always On is mandatory here

By default App Service unloads an idle app after ~20 minutes and reloads it on the next
request. For a request-driven API that's fine — a slightly slow first request.

RegimeDeck is not request-driven. It runs **11 `BackgroundService` instances** ingesting
FRED, Binance and Yahoo data and computing the screener and sector rotation on timers.
If the process is unloaded, *those stop too* — ingestion would only happen when somebody
happened to visit. Always On keeps the process resident by pinging it periodically.

This is also why every scale-to-zero platform was ruled out (Render's free tier, Cloud
Run's default, Azure Consumption). **The shape of your workload, not its traffic, decides
which hosting models are even eligible.**

## Why the quota problem was solved by moving region

A new Pay-As-You-Go subscription starts with a **`Total VMs` quota of 0** in many regions
— fraud prevention, not a billing problem. East US refused B1; a quota-increase ticket sat
unanswered for six days; Central US and East US 2 also refused. **France Central worked
instantly.**

The lesson generalises: on a new subscription, a zero VM quota usually reflects *regional
capacity*, and trying another region takes two minutes against days of waiting on support.
Try the cheap experiment before the expensive wait.

Region choice has real consequences — France Central means EU data residency (helpful for
GDPR) and ~80–120 ms extra latency for US visitors. For a macro dashboard, acceptable.

## App settings: what they are and the trap

App settings become **environment variables** in the container. .NET's configuration
provider reads them, mapping `__` (double underscore) to the `:` hierarchy separator, so
`ConnectionStrings__SqlServer` becomes `ConnectionStrings:SqlServer` in `IConfiguration`.
Colons don't survive as environment variable names on Linux, hence the convention.

```
ConnectionStrings__SqlServer   <full ADO.NET string>
Jwt__Secret                    <64 random bytes, base64>
Google__ClientId               <dedicated OAuth client id>
Resend__ApiKey / Resend__From
Fred__ApiKey
Frontend__Url                  https://regimedeck.com
ForwardedHeaders__Enabled      true
ASPNETCORE_ENVIRONMENT         Production
Features__ProEnabled           false
```

### ⚠ Why the connection string must NOT go in the "Connection strings" blade

App Service has a separate *Connection strings* section, which looks like the obvious home
for a connection string. It isn't — it applies **its own prefix** when creating the
environment variable: an entry of type SQLServer named `X` becomes `SQLCONNSTR_X`.

.NET then has a rule of its own: it strips `SQLCONNSTR_` and maps the remainder into the
`ConnectionStrings:` section. **Both sides add a prefix.** An entry named
`ConnectionStrings__SqlServer` therefore arrives as
`ConnectionStrings:ConnectionStrings:SqlServer` — which nothing reads.

The fail-fast validator then killed startup, and App Service served its placeholder page
with every route 404ing. Use **either** an App setting named `ConnectionStrings__SqlServer`
(what we did) **or** a Connection-strings entry named just `SqlServer` — never both
conventions at once.

### Why `ForwardedHeaders__Enabled=true` matters

App Service terminates TLS at its load balancer and forwards plain HTTP to your container,
with the original client IP and scheme in `X-Forwarded-For` / `X-Forwarded-Proto`. Without
telling ASP.NET Core to read those, two things break: the rate limiter sees the proxy's
single IP and throttles all users as one bucket, and `UseHttpsRedirection` believes every
request is insecure and can redirect-loop.

---

# Part IV — How deploying from GitHub actually works

Four distinct mechanisms, usually collapsed into "it deploys automatically".

## 1. Trust — OIDC instead of a stored secret

The old approach was a **publish profile**: a file with credentials, pasted into GitHub
secrets. It works, but it's a long-lived credential sitting in two places, and rotating it
means remembering it exists.

We used **user-assigned managed identity with federated credentials** (OIDC) instead:

1. Azure creates a managed identity — an Entra ID principal — and grants it rights on the web app.
2. Azure registers a **federated credential** on it that says, in effect: *"trust tokens
   issued by GitHub's OIDC provider whose subject is `repo:zdroo/SwingSignal:ref:refs/heads/master`."*
3. At run time, the workflow asks GitHub for a short-lived signed token describing itself
   (which repo, which branch). That's what `permissions: id-token: write` enables.
4. `azure/login@v2` presents that token to Entra ID, which validates the signature and
   checks the subject against the federated credential, then returns a short-lived Azure
   access token.

**No secret is stored anywhere.** The three repository values Azure adds — client ID,
tenant ID, subscription ID — are identifiers, not credentials; they're useless without a
GitHub token proving the workflow's identity. And the trust is scoped: a workflow on a
different branch gets a different subject and is refused.

This is why **basic authentication can stay disabled** on the web app. That warning during
creation ("may impact deployments") only applies to publish-profile deployment.

## 2. Build — producing the artifact

The `build` job compiles and calls `dotnet publish`, which produces a self-contained
folder: your DLLs, dependencies, `appsettings.json`, and a `.runtimeconfig.json` naming
the entry point and required runtime. That folder is uploaded as a workflow artifact so
the separate `deploy` job can download it — the two jobs run on different machines.

### ⚠ Why the generated workflow was wrong

Azure generates:

```yaml
- run: dotnet publish -c Release -o ${{env.DOTNET_ROOT}}/myapp
```

No project is named, so it resolves `RegimeDeck.slnx` — **six** projects, including
`RegimeDeck.Tests`. I predicted this would fail on `NETSDK1194`; **it didn't** — .NET 10
permits publishing a solution to a single output folder. It succeeded, which was worse
than failing: it silently shipped `coverlet.collector`, `Mvc.Testing`, `TestHost` and a
**second `.runtimeconfig.json`**.

That second runtimeconfig is what actually broke the site — see the startup mechanism
below.

The corrected workflow names the project and adds a test gate, since `ci.yml` runs in
parallel and cannot hold back a deploy:

```yaml
- name: Build
  run: dotnet build RegimeDeck.slnx --configuration Release      # warnings-as-errors gate
- name: Test
  run: dotnet test RegimeDeck.Tests/RegimeDeck.Tests.csproj --configuration Release --no-build
- name: Publish
  run: dotnet publish RegimeDeck.Api/RegimeDeck.Api.csproj -c Release -o publish
```

## 3. Transport — Kudu, zip deploy, and `wwwroot`

Every App Service has a hidden companion site at `<app>.scm.<region>.azurewebsites.net`
called **Kudu** — the deployment engine. It exposes a REST API for pushing packages,
browsing the filesystem, reading logs and running shell commands. `azure/webapps-deploy`
posts your zip to it.

Kudu unpacks it into **`/home/site/wwwroot`**, which is the application root — a
persistent network-mounted share, not part of the container image. That's why redeploying
doesn't rebuild a container: you're replacing files on a share the container reads.

> ### ⚠ Zip deploy merges; it does not replace
>
> This is the single least obvious fact in the whole process. Deploying **adds and
> overwrites files but never deletes** ones that are no longer in your package.
>
> So fixing the workflow was not enough. The six-project dump from the first run stayed in
> `wwwroot` and kept breaking startup, because the corrected package simply didn't mention
> those files. We had to empty the directory explicitly through Kudu's command API and
> redeploy:
>
> ```json
> {"command":"find /home/site/wwwroot -mindepth 1 -delete","dir":"/home/site"}
> ```
>
> `az webapp deploy --clean` returned Kudu 400, and zips built by PowerShell's
> `Compress-Archive` are rejected outright because they use backslash entry paths. Letting
> the Linux runner build the package was the reliable route.

## 4. Startup — how App Service decides what to run

For a Linux .NET app with no explicit startup command, the platform inspects `wwwroot`,
finds the `.runtimeconfig.json`, and runs the matching DLL.

**With two runtimeconfigs it cannot choose** — so it gives up and runs its default
handler, which serves `hostingstart.html`. That is exactly what we saw: `Server: Kestrel`
(the runtime *was* up), a 200 on `/`, and 404 on every real route. A running site serving a
placeholder is the signature of "platform can't find your app", not "your app crashed" —
a crash gives you 502/503 instead.

Setting an explicit **Startup Command** (`dotnet RegimeDeck.Api.dll`) removes the ambiguity
and is worth doing defensively.

## Why CI existing isn't the same as CI running

`ci.yml` triggered on `branches: [main]` while the backend repo uses **`master`** — so it
had **never executed once** since the repo split. Meanwhile the frontend's CI *was*
running and had been **red on every push for five days**, which nobody noticed, and that
same failure later blocked the first Vercel build.

**Confirm CI has actually run.** An empty Actions tab is not a clean bill of health, and a
consistently red pipeline stops carrying information.

---

# Part V — Domains, DNS and certificates

## What Cloudflare is doing here

Buying a domain gives you the right to say who answers questions about it. That's the
**nameserver** delegation: `regimedeck.com`'s registrar points at
`lady.ns.cloudflare.com` / `jacob.ns.cloudflare.com`, so Cloudflare is *authoritative* —
when any resolver on earth asks where `regimedeck.com` lives, Cloudflare answers.

A **zone** is the set of records for that domain. The ones we created:

| Record | Purpose |
|---|---|
| `@` CNAME → `<hash>.vercel-dns-017.com` | apex → Vercel |
| `www` CNAME → same | so www resolves at all |
| `api` CNAME → `<app>-<hash>.<region>-01.azurewebsites.net` | API → App Service |
| `asuid.api` TXT → verification id | proves you own the name |

**Why the TXT record exists.** Without it, anyone could add `api.theirdomain.com` pointing
at *your* app and serve their traffic through it, or claim a hostname you were about to
use. The `asuid.` TXT contains a value only your Azure subscription knows, so Azure can
confirm the person configuring DNS and the person owning the app are the same. It's proof
of control, not routing.

## Why a CNAME at the apex is unusual

DNS forbids a CNAME coexisting with other records at a zone apex, and an apex necessarily
has SOA and NS records. Historically you used an A record with a fixed IP.

Cloudflare works around this with **CNAME flattening**: it stores your apex CNAME but,
when queried, resolves the target itself and answers with the resulting **A records**. The
outside world sees a legal apex A record; you get to keep a name that follows your
provider's IPs. That's why `nslookup regimedeck.com` returned `64.29.17.65` rather than a
CNAME — and why Vercel can recommend a CNAME here at all. It's better than the legacy
`76.76.21.21` A record precisely because it doesn't pin you to one IP.

## Why every record must be "DNS only" (grey cloud)

Cloudflare's orange cloud means **proxied**: traffic goes to Cloudflare, which terminates
TLS with its own certificate and opens a second connection to your origin. It's a
deliberate, consensual man-in-the-middle for caching and WAF.

That breaks certificate issuance. Both Azure's managed certificate and Vercel's Let's
Encrypt certificate prove domain control by having the CA reach the name and see the
expected response. If Cloudflare is answering instead of your origin, validation fails —
and, worse, it fails again at **renewal**, months later, long after anyone remembers the
setting. Vercel's own DNS panel states `Proxy: Disabled` for this reason.

There's also no benefit to proxying here: App Service and Vercel both already terminate
TLS and Vercel already has a CDN, and caching does nothing for a JSON API.

## Why DNS strictly precedes certificates

It follows from the above: a certificate authority will not issue for a name it cannot
verify you control, and it verifies by resolving the name. So the order is always
**DNS record → hostname binding → certificate**. Attempting it in any other order simply
fails, which is why Vercel shows *"Invalid Configuration"* until records propagate, and
why the two Vercel certificates (apex, then www about a minute later) appeared separately.

## Apex vs www — one canonical origin

We chose the **apex**, and that choice propagates into four places that must match
character for character:

- `Frontend__Url` on the API — drives CORS **and** the Origin CSRF check
- `NEXT_PUBLIC_SITE_URL` — compiled into the frontend bundle
- Google OAuth **Authorized JavaScript origins**
- Vercel's primary domain

`www` then has to **redirect**, not serve. Vercel's dialog offers *"redirect apex domains
to www (recommended)"* — wrong here, since it makes www canonical. But **unchecking it
isn't sufficient**: www gets added as a plain alias that serves the site on its own
hostname, which is worse than either option. Sign-in from www sends
`Origin: https://www.regimedeck.com`, the API rejects it, and it presents as an
intermittent auth bug that depends on what the user typed. We verified this directly — a
refresh request with the www origin returns **403**.

Set www → **308 redirect** to the apex, and there is exactly one origin.

---

# Part VI — Vercel

## What it does

Vercel is a **build pipeline plus a CDN plus a serverless runtime**, connected to your
repository. On push it clones, installs, runs `next build`, and produces an **immutable
deployment** with its own URL. Domains are *aliases* pointed at a deployment — which is
why rollback is instant: it re-points an alias at an earlier immutable build rather than
rebuilding.

`next build` sorts routes into two kinds, visible in the build output:

- **Static (○)** — rendered at build time into HTML served from the CDN. `/`, `/dashboard`,
  `/macro` and `/playbook` are static with a 5-minute revalidate, so they fetch your API
  *during the build* and periodically thereafter.
- **Dynamic (ƒ)** — rendered per request in a serverless function. `/odds/[symbol]`, since
  the symbol isn't known ahead of time.

## Why environment variables must exist before the first build

`NEXT_PUBLIC_*` variables are **inlined into the JavaScript bundle at build time** — they
are not read at runtime. Two consequences:

1. **Adding one later has no effect until you rebuild.**
2. **They are public.** Anyone can read them in the bundle. Never put a secret behind that
   prefix; the client ID is fine, a client *secret* would not be.

### ⚠ Why "the pages render" doesn't prove the API URL is set

`lib/api.ts` falls back to `https://localhost:7260` when `NEXT_PUBLIC_API_URL` is missing.
Static pages fetch at *build* time on Vercel's builder, so they'd still render perfect
data — while every *client-side* call (auth, odds lookups) silently pointed at localhost.
The site would look flawless and authentication would be completely broken.

So verify the bundle itself, not the rendered page:

```bash
curl -s https://regimedeck.com/auth | grep -oE '/_next/static/chunks/[^"]+\.js' | sort -u |
  while read c; do curl -s "https://regimedeck.com$c" | grep -q "api.regimedeck.com" && echo "found in $c"; done
```

We confirmed `api.regimedeck.com` present and **zero** references to `localhost:7260`.

### ⚠ The build failure that dev could never have caught

```
useSearchParams() should be wrapped in a suspense boundary at page "/account"
```

`useSearchParams()` depends on information that doesn't exist during prerendering, so it
opts its component tree into client-side rendering — and Next requires a `<Suspense>`
boundary marking where that switch happens, so it can prerender everything above it and
stream the rest.

**`next dev` never prerenders**, so the page worked perfectly in development. Only
`next build` fails. This is the archetypal case for running the production build in CI:
a whole class of Next.js errors exists only in that step.

---

# Part VII — Google OAuth, and the origin model that ties it together

## Why a dedicated OAuth client

Development had been borrowing another project's client ID. That conflates two
applications' users and consent screens, and means one project's OAuth settings can break
the other. Production gets its own.

Configuration is minimal because of the flow in use:

- **Authorized JavaScript origins:** `https://regimedeck.com` (plus `http://localhost:3000`)
- **Authorized redirect URIs:** *empty*

Why empty: `@react-oauth/google` uses a **popup/JS flow**, where Google returns a credential
to JavaScript on the page. There is no server redirect, so redirect URIs are never
consulted. Google validates the **origin** the browser reports — hence origins matter and
must match exactly, including scheme and absence of a trailing slash.

The same client ID appears in three places: the OAuth app, `Google__ClientId` on the API
(which validates the token server-side), and `NEXT_PUBLIC_GOOGLE_CLIENT_ID` on Vercel.

## Why the auth cookie design is what it is

Worth understanding because it explains several settings that otherwise look arbitrary.

The frontend is `https://regimedeck.com`; the API is `https://api.regimedeck.com`. Same
registrable domain, **different origins** — so browser requests between them are
cross-origin and need CORS, including `Access-Control-Allow-Credentials: true` for the
cookie to travel.

The access token lives **only in memory** (a module variable, never `localStorage`), so
XSS cannot read it. Persistence comes from a refresh token in an **HttpOnly** cookie —
invisible to JavaScript entirely. On reload, the in-memory token is gone and the app calls
`/auth/refresh`, which the cookie authenticates.

`SameSite=None` is set so the cookie is sent on these cross-origin requests. But `None`
removes the CSRF protection `SameSite` normally provides — so the API adds an explicit
**Origin check** on cookie endpoints, which is why `Frontend__Url` must match the real
origin exactly. That's the whole chain: *cross-origin architecture → SameSite=None →
lost CSRF defence → explicit Origin validation → one canonical origin, enforced by the www
redirect.*

Verified against the live API, and worth re-running after any origin change:

```bash
# preflight from the real origin → 204, Allow-Credentials: true
curl -i -X OPTIONS https://api.regimedeck.com/api/auth/refresh \
  -H "Origin: https://regimedeck.com" -H "Access-Control-Request-Method: POST"

curl -X POST .../api/auth/refresh -H "Origin: https://regimedeck.com"      # 401 — no cookie
curl -X POST .../api/auth/refresh -H "Origin: https://evil.example.com"    # 403 — rejected
curl -X POST .../api/auth/refresh -H "Origin: https://www.regimedeck.com"  # 403 — why www redirects
```

In the browser: sign in, **hard reload** (must stay signed in, `POST /auth/refresh` → 200),
log out (next refresh → 401). When a reload logs users out, the status code localises the
fault: **401** = cookie not sent (`SameSite`/`Secure`/forwarded headers), **403** = origin
mismatch.

---

# Part VIII — Validating before and after

## Run production config locally, before deploying

Point a local process at the production database with `ASPNETCORE_ENVIRONMENT=Production`:

```powershell
$env:ASPNETCORE_ENVIRONMENT       = 'Production'
$env:ConnectionStrings__SqlServer = '<prod connection string>'
# ...remaining required settings
dotnet run --project RegimeDeck.Api --no-launch-profile
```

Two reasons this is worth doing. First, it exercises the real startup path — config
fail-fast, EF migrations, seeding, ingestion — **without spending a deploy cycle** on each
typo. Second, it leaves the database **warm**: this run applied 12 migrations, seeded 11
assets and ingested ~74k candles and ~144k macro points, so the first real deploy started
against populated data instead of an empty schema.

What it can't cover: anything requiring real HTTPS — the `Secure` cookie and the
cross-origin checks. Those wait for Phase 4.

## Diagnosing App Service without the Portal

The Portal cannot show you `wwwroot` or the container log. These were decisive:

```powershell
az webapp config appsettings list -g <rg> -n <app> --query "[].name"   # names only, no secrets
az webapp config connection-string list -g <rg> -n <app>               # found the misplaced entry
az webapp log deployment list -g <rg> -n <app>                         # status 4 = success

# Kudu shell: list wwwroot, grep logs
az rest --method post --uri "https://<scm-host>/api/command" \
        --resource "https://management.core.windows.net/" \
        --headers "Content-Type=application/json" --body '@cmd.json'
```

Unhandled-exception stack traces live in `/home/LogFiles/StartupLogs/*_success.log`, not
the `*_docker.log` (which carries platform messages). Enable app logging first with
`az webapp log config --application-logging filesystem --docker-container-logging filesystem`.

## A 500 immediately after first boot is probably not a bug

`/api/regime/current` timed out at 30 s during the initial backfill. The index existed and
DTU sat at 17% — pure contention with the ingestion writes. It returned in **~1 s** once
ingestion settled. Similarly `/api/screener` is empty until `ScreenerComputeService` clears
its 10-minute startup delay. **Re-test before investigating.**

---

# Part IX — What went wrong, and the pattern

Nine issues. **Six presented as the same symptom** — a running site serving Azure's
placeholder page:

| # | Problem | Root cause |
|---|---|---|
| 1 | B1 create refused | Zero VM quota — regional capacity, not the account |
| 2 | 4× the cost | Web App created on Windows; OS is fixed at plan level |
| 3 | App wouldn't start | Connection string in the wrong blade → double-prefixed key |
| 4 | Placeholder page | Published the solution → test DLLs + 2 runtimeconfigs |
| 5 | Fix didn't take | Zip deploy merges; old files persisted |
| 6 | No CI signal | Backend workflow triggered on `main`; repo uses `master` |
| 7 | Vercel build failed | Frontend CI had been red for 5 days, unnoticed |
| 8 | Only failed in build | `useSearchParams` without Suspense; `next dev` never prerenders |
| 9 | 403 on sign-in | `www` added as an alias instead of a redirect |

Three patterns worth carrying forward:

**Green does not mean working.** Both Azure deploy runs went green while the site was
dead. The certificate was issued while the app was broken. Verify an endpoint returning
real data — never a pipeline's own status.

**The same symptom rarely has the same cause.** Six of these looked identical from the
browser. Progress came from tools that reveal *state* rather than *outcome*: listing
`wwwroot`, listing app-setting names, reading the container log. Get to state as early as
possible — the hours lost here were mostly spent guessing before installing `az`.

**Both sides of every reference must agree, and each side may transform it.** The
connection string failed because App Service *and* .NET each added a prefix. The origin
checks failed because www differs from the apex by four characters. Deployment is mostly
string-matching across system boundaries, and it's worth reading each side's
transformation rules rather than assuming the string arrives as written.
