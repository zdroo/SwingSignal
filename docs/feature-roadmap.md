# RegimeDeck — Feature Development Roadmap

Derived from the July 13 2026 feature/monetization assessment. Sequenced so
each item ships independently, reuses the existing engine, and does not change
working behaviour. Take them **one at a time, top to bottom.**

## Guiding rules (apply to every phase)

1. **Additive by default.** Each feature is a new entity + table (own migration),
   a new Application service, a new API controller, and new FE pages/components.
   Nothing existing is deleted or rewired.
2. **Reuse by calling, not by editing.** New services depend on the existing
   `IHistoricalOddsService`, `IMacroRegimeService`, `RegimeInsight`, `TradeRead`
   through their current interfaces. Those files are not touched.
3. **Changes to existing code are allowed only if they are a strict improvement
   or bug fix**, are covered by a test, and keep the old behaviour for old
   callers (append record fields, add optional params, extend a switch — never
   change a signature a caller relies on).
4. **Expensive work is precomputed.** The odds engine is costly per asset, so
   anything that runs it across many assets is a scheduled background service
   writing to a cache table (the pattern already used by ingestion / alerts),
   with the API reading the cache. Never compute a universe on a request thread.
5. **Gating reuses what exists.** `ProFeatures` dark-launch flag + `ProOnly`
   policy. No new auth machinery.
6. **Tests per feature in their own files.** Existing test files are not edited
   (except the shared `ApiFactory`, additively — as billing already did).

Reference points in the current code:
- Odds per asset: `IHistoricalOddsService.GetOddsAsync(symbol)` → `AssetOddsDto`
  (carries `ThreeMonths` odds + `TradeRead`). This is the screener's unit call.
- Regime synthesis: `RegimeInsight.ComputePlaybook` / `ComputeMarketHealth`.
- Background service pattern + registration: `RegimeDeck.Infrastructure`
  `DependencyInjection` `includeBackgroundServices` block; e.g.
  `AlertEvaluationService`, `WeeklyReportService`.
- Asset universe seed: `AssetSeeder.DefaultAssets`.

---

## Phase 1 — Regime Screener  ★ the Pro-justifying anchor

**Goal.** "Which assets does the current regime favour right now?" — the whole
tracked universe ranked by edge/stance, filterable. Free users see a limited
teaser (top asset-class + a few flagship tickers); Pro sees the full universe
with filters.

**New code (additive).**
- Domain: `ScreenerRow` entity (Symbol, Name, MarketType, CurrentPrice,
  Odds3M, BaseRate3M, Edge3M, Stance, Strength, ComputedAt) — a cache table.
- Contracts: `ScreenerRowDto`, `ScreenerResultDto(rows, asOf, universeSize)`.
- Application: `IScreenerService` + `ScreenerService` (reads the cache,
  applies free/Pro trimming + filters). `ScreenerUniverse` static list (start
  with the seeded assets + the sector ETFs from Phase 2 when they exist).
- Infrastructure: `ScreenerComputeService : BackgroundService` — every N hours
  loops the universe calling `IHistoricalOddsService.GetOddsAsync`, upserts
  `ScreenerRow`s. Mirrors `AlertEvaluationService` exactly (scope per run,
  `AppException` per-asset swallow, startup delay).
- API: `ScreenerController` — `GET /api/screener` (free: trimmed;
  `[EnableRateLimiting("odds")]`), `GET /api/screener/full` (`ProOnly`).
- FE: `/screener` page + `ScreenerTable` component (reuses the stance chips and
  edge formatting already in `TradeReadCard` / watchlist). Navbar entry.

**Reused unchanged.** Odds engine, `TradeRead`, rate-limit policies,
`ProFeatures`/`ProOnly`, background-service pattern, DTO/records style.

**Changes to existing (minimal, safe).**
- `DependencyInjection`: register `ScreenerComputeService` + `ScreenerService`
  (additive lines).
- `SwingSignalDbContext`: add `DbSet<ScreenerRow>` (additive) + new migration.
- Navbar: add a link (additive array entry).

**Decoupling.** The screener only ever *reads* `AssetOddsDto`. If the odds
engine changes, the screener keeps working. The cache table is owned solely by
this feature; nothing else reads or writes it.

**Risk & mitigation.** Main risk is compute cost / external-API load from
looping the universe. Mitigations: (a) it's a background job, never a request;
(b) start the universe small (~20-30 assets) and cap it; (c) reuse the existing
per-asset ingestion so no new data source; (d) stale cache is fine — show
`asOf`. No existing endpoint is touched, so nothing can regress.

**Tests.** `ScreenerServiceTests` (free-trim vs full, filter logic — pure).
Integration: `/api/screener` returns rows; `/api/screener/full` is `ProOnly`
(401/403/200) mirroring `WatchlistTests`. Compute service logic kept in a pure
helper so it's unit-testable without the timer.

**Effort.** Medium (largest of the set, but almost all new files).

---

## Phase 2 — Sector Rotation

**Goal.** Which of the 11 S&P sectors the regime favours + relative strength —
a heatmap. Free: current snapshot. Pro: history + rotation-shift alerts.

**New code.**
- Seed the 11 SPDR sector ETFs (XLK, XLF, XLE, XLV, XLI, XLY, XLP, XLU, XLB,
  XLRE, XLC) into `AssetSeeder.DefaultAssets` (additive entries → they flow
  through the existing ingestion automatically).
- Domain: `SectorRotationRow` (Sector, Symbol, RegimeFit 0-100, RelStrength,
  Rank, ComputedAt) cache table.
- Application: `ISectorRotationService`; a `SectorRotation` computer that reuses
  the screener rows for the sector ETFs + a relative-strength calc from candles
  (`ICandleRepository`, existing).
- Infrastructure: fold the computation into `ScreenerComputeService` (same loop
  already fetches these symbols) or a sibling service — keep it a separate
  writer to its own table.
- API: `GET /api/sectors` (free snapshot), history behind `ProOnly`.
- FE: `SectorHeatmap` component + a dashboard/`/macro` placement.

**Reused unchanged.** Screener rows, candle repo, `RegimeInsight` colour/label
conventions, `MarketHealth` meter styling.

**Changes to existing (minimal, safe).** `AssetSeeder` list (additive);
`DbSet` + migration; nav/section link.

**Decoupling.** Reads screener rows + candles only. Owns its cache table.

**Risk & mitigation.** The sector ETFs need history ingested once (handled by
existing on-demand + scheduled ingestion). Heatmap is descriptive, not
predictive — same honesty framing as the playbook. No existing behaviour
changes.

**Tests.** `SectorRotationTests` (fit/rank ordering, pure). Integration for the
snapshot endpoint + Pro gate on history.

**Effort.** Small–medium (rides Phase 1's compute loop).

---

## Phase 3 — Richer Alerts

**Goal.** Extend alerts beyond stance-flip + health-band to what traders ask for
most: edge turned positive, price-target crossed, a new asset entering
"Long bias" in the screener, and macro-event reminders for watchlist assets.

**New code.**
- New `AlertRules.Change` producers: `EdgeTurnedPositive`,
  `PriceTargetCrossed`, `NewLongBiasEntrant` (needs Phase 1 screener),
  `EventReminder` (needs Phase 4). Each is a new pure method beside the existing
  `HealthChange` / `StanceChange` — the existing two are untouched.
- New `AlertState` keys (the store already keys by arbitrary string).

**Reused unchanged.** `AlertEvaluationService` loop, `AlertEmailBuilder`,
`AlertState` store, the 20h hysteresis, opt-out toggle.

**Changes to existing (additive, improvement).** `AlertEvaluationService.
EvaluateAsync` gains extra checks in its existing per-user / per-symbol loops —
new branches only, old branches unchanged and still tested. `AlertRules` gains
new methods; the existing methods and their tests are not modified.

**Decoupling.** Each new rule is independent and pure; the evaluator composes
them. A new rule failing/being removed cannot affect the others.

**Risk & mitigation.** The hysteresis + first-run-silent contract already
prevents spam; new rules follow the same `Notify=false when previous is null`
convention (enforced by tests). Purely additive to the digest.

**Tests.** Extend `AlertRulesTests` with the new producers (new `[Fact]`s, not
edits to existing ones).

**Effort.** Small. (Do after Phase 1; the event-reminder sub-rule waits for
Phase 4.)

---

## Phase 4 — Economic-Event Awareness

**Goal.** On each asset page: "next high-impact macro event: CPI in 3 days" and
(for stocks) days-to-earnings, so users don't trade blind into a release.

**New code.**
- Infrastructure: reuse `FredApiClient` for FRED **release dates**
  (`/release/dates`) to build a forward macro-event calendar (CPI, NFP, FOMC…).
  Earnings dates need a separate free source (Yahoo `quoteSummary`
  earningsDate, or Finnhub free) — new small client, isolated.
- Domain: `EconomicEvent` (Title, Date, Impact, Kind) cache table +
  a lightweight ingestion service (12–24h cycle).
- Contracts/API: `GET /api/events/upcoming` and an optional per-asset earnings
  field.
- FE: a small "next event" line on the asset page + optional markers.

**Reused unchanged.** FRED client, ingestion/background pattern, asset page.

**Changes to existing (additive).** Optionally append an `EarningsDate?` field
to `AssetOddsDto` (append-only record param with default → existing callers
unaffected). New `DbSet` + migration.

**Decoupling.** Its own calendar table + client. The asset page reads it via a
new endpoint; if events are unavailable the page renders exactly as today.

**Risk & mitigation.** New external source (earnings) is the only real unknown —
keep it optional and fail-soft (no event line if the fetch fails). Macro events
via FRED reuse an existing, trusted client.

**Tests.** Calendar-builder unit tests (next-event selection, impact sort).
Fail-soft integration (missing data → empty, not error).

**Effort.** Medium (the earnings source is the variable).

---

## Phase 5 — Free-tier funnel adjustments

Small monetization/UX changes that *add* to the free tier (never shrink it).

- **Limited free watchlist (~3 assets).** The one genuinely invasive change:
  the watchlist controller is currently `[Authorize(Policy = "ProOnly")]` on the
  whole class. Plan: keep the class `[Authorize]` (any account), move the Pro
  boundary *inside* — Free capped at 3 items and no alerts, Pro at 15 + alerts,
  enforced in `WatchlistService` via `ProFeatures`. This is an improvement
  (funnel) but touches working code, so it gets its own PR, its own tests, and
  keeps every current Pro behaviour identical (15 cap, alerts) — only a new,
  smaller Free path is added. **Do this deliberately, not bundled.**
- **"How accurate is this?" trust line** on asset pages — reads existing
  backtest data; pure FE addition.
- **Screener/sector teasers** already built as the free views in Phases 1–2.

**Effort.** Small, but the watchlist split is the highest-touch change in the
whole roadmap — treat it with care and full regression tests.

---

## Phase 6 — Later (Phase-2 monetization, different buyers)

- **API access tier** — expose existing read endpoints behind API keys + a new
  rate-limit policy. Additive (new auth path parallel to JWT); no changes to the
  web endpoints.
- **CSV / data export** — serialize existing DTOs; new endpoints only.
- **Portfolio regime-fit** — upload holdings → reuse the screener/odds to score
  each. New page + endpoint; reads the engine, owns nothing else.

---

## Sequencing rationale

1. **Screener first** — highest value (makes Pro worth $14), biggest funnel/SEO
   surface, and it builds the precompute loop the next phases reuse.
2. **Sector rotation** — rides that loop; cheap, on-brand, shareable.
3. **Richer alerts** — the retention multiplier; now able to alert on screener
   entrants.
4. **Event awareness** — independent; slot when convenient.
5. **Free-tier tuning** — once the paid anchor exists, tune the funnel.
6. **API/export/portfolio** — when revenue justifies the different buyer.

## Explicitly NOT building (scope discipline)

Full charting (TradingView), trading journal (TradeZella), real-time intraday
data (RegimeDeck is a daily/swing tool), AI "buy/sell signals" (contradicts the
honest-calibration brand).
