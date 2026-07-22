# Odds Methodology & Math Validation

**What this document is:** every number RegimeDeck labels as "odds", "edge",
"base rate", "price target", "relative strength" or a backtest metric — traced to
its exact formula, with the source location and a validation verdict. Organised
as: the **one core engine** (Part I), then **per endpoint** (Part II), then **per
page** (Part III), since several pages share endpoints. Part IV is the at-a-glance
validation table; Part V lists the interpretation caveats that matter.

> **The golden rule:** there is exactly **one** odds computation —
> `HistoricalOddsService.GetOddsAsync` (`RegimeDeck.Application/Odds/HistoricalOddsService.cs`).
> The screener, sector board, watchlist, popular strip and per-asset page all
> read their odds/edge/base-rate from it (via `ThreeMonthSummary.From`). Nothing
> re-derives odds independently, so the same asset shows the same numbers
> everywhere. This was verified live in an earlier review (SPY, BTC, GLD, XLK
> matched to the decimal across screener/sector/odds).

Notation: `N` = horizon in calendar days (30 / 90 / 180, or 7–365 for the custom
window). "Analog" = a historical month whose macro fingerprint resembles today.

---

## Part I — The core odds engine

Pipeline for one horizon `N`, in order. Every step lists **formula**, **source**,
**verdict** (✔ correct / ⚠ correct-but-note).

### 1. Monthly macro snapshots
`MacroSnapshotBuilder.BuildUncachedAsync`

- Each indicator is forward-filled to a **monthly** series (last known value
  carried forward within a month).
- **Trending levels** (CPI, GDP, Gold, Oil, Fed balance sheet, M2, Core PCE,
  retail sales, housing starts, copper, + crypto levels) are converted to
  **year-over-year %**:
  `yoy(t) = (x(t) − x(t−12mo)) / |x(t−12mo)| × 100`, only when `x(t−12mo) ≠ 0`.
- **6-month momentum** dimensions are derived on the *transformed* series:
  `mom6(t) = x'(t) − x'(t−6mo)` (so CPI momentum = change in the inflation rate).
- A snapshot is kept only if it has **≥ 8** indicators present (`MinSnapshotIndicators`).

**Verdict ✔.** YoY uses `|baseline|` so a negative baseline can't flip the sign;
the YoY set are all strictly-positive levels anyway. Momentum-on-transformed is
the intended "change in rate, not change in level".

### 2. Normalisation to z-scores
`ComputeNormalizationStats` + `BuildZScoreVector`

For each dimension, over the **visible** snapshots (only data up to the "as-of"
month — no lookahead), needing ≥ 12 observations:
`mean = avg(x)`, `std = sqrt(avg((x − mean)²))` (population std),
`z = (x − mean) / std` (dimensions with `std = 0` are dropped).

**Verdict ✔.** Population (not sample) std is fine for normalisation. Stats are
computed from visible history only, which is what makes the backtest honest.

### 3. Distance & similarity between two months
`SharedDimensionDistance` → similarity in `FindMatches`

Over the dimensions **both** months share, grouped into 8 families (policy,
rates, inflation, labor, growth, stress, commodities, crypto-native):

- within family `f`: `MSD_f = Σ (z_a − z_b)² / count_f`
- across families: `D² = mean_f(MSD_f)` → **distance** `D = √(mean_f MSD_f)`
- **similarity** `S = 100 / (1 + D)`

Validity floor: ≥ 8 shared dimensions and ≥ 5 shared families (relaxed for the
crypto profile, which spans fewer families). Family weighting stops five
correlated rate series from dominating the distance 5-to-1 over, say, all of labor.

**Verdict ✔.** Each family contributes equally regardless of member count; `S` is
monotonic-decreasing in `D`, bounded `(0, 100]`, `D=0 ⇒ S=100`.

### 4. Analog selection
`FindMatches` (called via `MacroRegimeService.FindSimilarPeriodsAsync`)

- Candidates = visible months **≥ 6 months older** than today (so an analog's
  forward outcome is fully observed) and, for crypto, **≥ the asset's first
  candle** (`FloorAnalogsToAssetHistory` — analogs before listing can't be scored).
- Sort candidates by `S` descending.
- **Decluster** greedily: take the best, then skip any candidate within
  **6 months** of one already taken (one macro event, e.g. late-2008, can't fill
  several slots). Keep up to **`AnalogCount = 40`**.
- **Rank** = the analog's position in the *full* sorted candidate list;
  **TopPercent** = `rank / candidateCount × 100` (rank 3 of 430 ⇒ top 0.7%).

**Verdict ✔.** Declustering preserves true rank; the 6-month barrier is applied on
both candidate eligibility and spacing.

### 5. Kernel weighting
`MacroSnapshotBuilder.KernelWeights`

Recover distance from similarity `d = 100/S − 1`, then a Gaussian kernel with
**bandwidth = median analog distance**:
`w_i = exp(−(d_i / bandwidth)²)`.

**Verdict ✔** (with note ⚠). Close analogs dominate, far ones fade smoothly
instead of a hard top-K cliff. **Note:** the kernel consumes the similarity as it
appears in `HistoricalMatchDto.SimilarityScore`, which was **rounded to 1 decimal**
in `FindSimilarPeriodsAsync`. The re-derived `d` therefore carries ≤ 0.1-similarity
rounding error — negligible for a smooth kernel, but it means the weight isn't
computed from full-precision distance.

### 6. Analog forward returns
`ComputeReturns` + `CandleMath`

For each weighted analog month:
- entry = candle nearest the month, within **7 days** (`FindNearest`);
- exit = candle nearest `month + N days`, within `ExitWindow = clamp(N/3, 2, 7)` days;
- `return = (exit.Close − entry.Close) / entry.Close × 100` (rounded 2dp).

**Verdict ✔.** Tight exit window stops a short horizon from matching an exit candle
sitting next to entry. Analogs whose entry/exit can't be located are dropped.

### 7. Headline odds (the "% chance up")
`ComputeOdds`

- `rawOdds = Σ(w : return > 0) / Σw × 100` (weighted share of positive analogs).
- **Shrink toward the base rate** (production uses the fixed factor, `ShrinkagePrior`
  is null): `PositiveOdds = baseRate + 0.4 × (rawOdds − baseRate)` (`Shrinkage = 0.4`),
  rounded 1dp.
- If **no base rate** is available (very thin history): `PositiveOdds = rawOdds`
  (unshrunk).

**Verdict ✔** (with note ⚠). Shrinkage bounds `PositiveOdds` between the base rate
and the raw odds; since inputs are in `[0,100]` so is the output. **Note:** the
displayed odds are **post-shrinkage** — deliberately humbler than the raw analog
count. See §7-caveat in Part V.

### 8. Base rate
`CandleMath.ComputeBaseRate`

Share of this asset's own **N-day rolling windows** that closed positive:
sample every **21 candles** (`MonthlyStride`) a window of `HorizonCandles(N) =
max(1, ⌊N × 5/7⌋)` trading candles; positive if `exit.Close > entry.Close`.
Needs **≥ 24** windows (`MinBaseRateSamples`) or returns null. Production spans
**all** history (the trailing-years cutoff is plumbing, off in production).

**Verdict ✔** (with note ⚠). The share-positive point estimate is unbiased.
**Note:** windows **overlap** (stride 21 « horizon ≈ 64 for 90d), so "≥ 24 samples"
overstates the number of *independent* observations — it widens the true
confidence interval but does not bias the base rate itself.

### 9. Edge
`ComputeOdds`

`Edge = PositiveOdds − baseRate` (rounded 1dp); `0` when no base rate.

Because `PositiveOdds = baseRate + 0.4(raw − baseRate)`, algebraically
**`Edge = 0.4 × (rawOdds − baseRate)`** — the displayed edge is 40% of the raw
analog edge. This is the single number the app treats as "what the regime adds".

**Verdict ✔.** Consistent with the shrinkage; sign preserved.

### 10. Return distribution stats
`ComputeOdds` + `WeightedPercentile`

- `AverageReturn = Σ(return × w) / Σw` (weighted mean, 2dp).
- `MedianReturn = ` weighted P50; `BestCase / WorstCase = max / min` analog return.
- **Weighted percentile**: with analogs sorted ascending by return, walk cumulative
  weight and return the first return where `Σw ≥ totalWeight × p`.

**Verdict ✔** (with note ⚠). Percentile is the standard weighted lower-value
convention. **Note:** Best/Worst are **unweighted extremes** (the single most
extreme analog, regardless of its kernel weight), whereas odds/mean/median/targets
are weighted — an intentional but real definitional difference. `TotalCases` and
`PositiveCases` are likewise **raw counts**, so `PositiveCases / TotalCases` will
not equal the weighted, shrunk `PositiveOdds`.

### 11. Price targets
`ComputeOdds`

`PriceTarget{Low,Mid,High} = currentPrice × (1 + {P25,P50,P75}/100)`, using the
same weighted percentiles of the analog return distribution (2dp).

**Verdict ✔** (with note ⚠). Internally consistent with the median. **Note:** these
are **percentiles of the analog outcomes**, not guaranteed bands — in a bearish
regime even `PriceTargetHigh` (P75, labelled "Optimistic") can sit **below** the
current price. The label denotes distribution position, not a guaranteed gain.

### 12. Statistical read (stance & strength)
`TradeRead.Compute`

Over horizons that have cases and a base rate: `best` = max-edge horizon,
`worst` = min-edge horizon. With `EdgeThreshold = 5`, `MinActionableOdds = 55`,
`StrongEdge = 8`:

- **Long bias** if `best.Edge ≥ 5` **and** `best.PositiveOdds ≥ 55`;
- **Stand aside** if `worst.Edge ≤ −5` **and** `best.Edge < 5`;
- else **No edge**.
- **Strength**: `Weak` if matches < 15; `Strong` if `|edge| ≥ 8` and matches ≥ 25;
  `Moderate` if `|edge| ≥ 5`; else `Weak`.

**Verdict ✔.** Pure thresholds on the (post-shrinkage) edge; because `Edge` is
already 40% of raw, the `≥ 5` gate implies a raw analog edge ≈ 12.5pp — a
deliberately conservative bar.

### 13. Above/below-MA200 breakdown *(descriptive only)*
`HistoricalOddsService.ComputeBreakdown`

Splits the analogs by whether the asset was above/below its **200-day simple
average** (`mean of last 200 closes`) at each analog date, and reports each
group's share-positive and median 3-month return.

**Verdict ✔, but non-headline.** This split is **descriptive context only** — it
looked predictive in-sample but failed walk-forward validation, so it does **not**
influence `PositiveOdds`. (Same status: asset-state conditioning and crypto-cycle
conditioning — `AssetStateCalculator` / `CryptoCycle` — are plumbing whose
bandwidths are **null in every production profile**, so they multiply every weight
by 1 and change nothing live. They exist for backtest experimentation via the
`stateH` / `cycleH` parameters.)

---

## Part II — Per endpoint

Each endpoint's odds fields and how they're produced (all reference Part I).

### `GET /api/regime/odds/{symbol}` → `AssetOddsDto`
The full per-asset read. Runs Part I for **three** horizons:
- `OneMonth` (N=30), `ThreeMonths` (N=90), `SixMonths` (N=180) — each an
  `OddsForPeriodDto` (§7–§11): PositiveOdds, Edge, BaseRate, Average/Median/Best/
  Worst return, PriceTarget Low/Mid/High, Total/PositiveCases.
- `MatchesUsed` = analog count (§4). `CurrentPrice` = latest close.
- `TradeRead` (§12), `Breakdown` (§13), `Explanations` (narrative, non-numeric).
- Crypto uses the crypto dimension subset and the short-horizon profile for N≤45
  (`MatchingOptions.ForMarket`); the math is otherwise identical.

### `GET /api/regime/odds/{symbol}/period?days=N` → `AssetPeriodOddsDto`
Part I for **one** user-chosen horizon `N ∈ [7,365]`. Same `OddsForPeriodDto`
(§7–§11). Auth-gated; recomputes on demand.

### `GET /api/regime/matches?topK` → `HistoricalMatchDto[]`
The analog list itself (§3–§4): `SimilarityScore = S` (1dp), `TopPercent =
rank/candidateCount×100`, and each analog's indicator z-cluster values. No odds.

### `GET /api/regime/current` → `MacroRegimeDto`
**Not odds.** Descriptive regime snapshot: per-indicator signal/tone/severity,
composite market-health score, rule-based narrative, playbook. Plays **no** role
in the odds engine (explicitly separate). Cached 10 min.

### `GET /api/screener` and `/screener/full` → `ScreenerResultDto`
Per-asset **3-month** summary from the cached board. Each row
(`ScreenerRowFactory.Create` → `ThreeMonthSummary.From(GetOddsAsync)`):
`Odds3M = ThreeMonths.PositiveOdds`, `BaseRate3M`, `Edge3M`, `Stance`, `Strength`,
`CurrentPrice` — i.e. exactly the §7/§8/§9/§12 values for N=90. Rows are ranked by
`Edge3M` desc (`EdgeRanking.ByEdgeDescending`, null edges sink). Free tier is
trimmed to `FreeSymbols`; filters (stance/market/minEdge) are Pro and applied
post-hoc, never re-computing odds.

### `GET /api/sectors` → `SectorRotationResultDto`
Per-sector-ETF **3-month** summary (same `ThreeMonthSummary`) **plus relative
strength** (`SectorRotationRowFactory`): `Odds3M`, `Edge3M`, `Stance`, and
`RelStrength3M` = **asset 90-day return − SPY 90-day return** in percentage points
(`RelativeStrength.Compute`, lookback `RelStrengthLookbackDays = 90`, benchmark
`SPY`). Ranked by `Edge3M` desc.

- `ReturnOver`: `(last.Close − start.Close)/start.Close × 100`, `start` = first
  candle at/after `last − 90d`. **Verdict ✔.**

### `GET /api/backtest/{symbol}` and `/compare` → `BacktestResultDto`
Walk-forward calibration of the engine itself (`BacktestService.RunCore`), no
lookahead (stats & analogs use only data before each evaluation month):
- `TotalPredictions`, `FirstPrediction`, `LastPrediction`.
- **DirectionalAccuracy** = `#(predictedOdds ≥ 50 ⇔ actualPositive) / n × 100`.
- **BrierScore** = `mean((p − outcome)²)` with `p = predictedOdds/100`, `outcome ∈
  {0,1}` (0 = perfect, 0.25 = always 50%).
- **Calibration** buckets: for each predicted-odds band, `AvgPredictedOdds` vs
  `ActualPositiveRate` (reliability curve).
- `AvgPredictedOdds`, `ActualPositiveRate = #positive/n × 100`.
- `/compare` runs the same twice (naive baseline vs current config) and diffs Brier
  & accuracy.

**Verdict ✔.** Standard Brier and reliability definitions; the walk-forward
construction (warmup 60 months, ≥ 5 usable analogs) is lookahead-free.

### `GET /api/assets/popular` → `PopularAssetDto[]`
Per flagship symbol (`PopularAssetsService`): `ChangePct` = 90-day price change,
`Spark` = downsampled closes, and **`Odds3M / BaseRate3M / Edge3M` from
`GetOddsForDaysAsync(symbol, 90)`** — the same §7/§8/§9 values for N=90. Cached
5 min; odds failures degrade the card to price-only.

### `GET /api/watchlist/overview` → `WatchlistRowDto[]` *(Pro)*
Per watchlist asset: the **3-month** `ThreeMonthSummary` of `GetOddsAsync`
(`Odds3M`, `BaseRate3M`, `Edge3M`, `TradeRead`) + current price. One odds
computation per row.

---

## Part III — Per page

Pages, the endpoints they call, and the odds they surface. (Endpoints appear once
in Part II; shared ones are cross-referenced.)

### `/odds/[symbol]` — the per-asset page (the odds showcase)
- **`GET /api/regime/odds/{symbol}`** → 1M/3M/6M outlooks (`PeriodTargets`), the
  P25/P50/P75 **price-target cards** ("Conservative / Base Case / Optimistic"),
  the **TradeRead** card (stance + strength + reasons), and the MA200 breakdown.
- **`GET /api/regime/odds/{symbol}/period`** (`PeriodPredictor`) → custom-horizon
  odds + targets.
- **`GET /api/backtest/{symbol}`** (`BacktestPanel`) → accuracy/Brier/calibration —
  the "how accurate is this?" proof.
- `GET /api/candles/{symbol}` + `GET /api/regime/matches` (`PriceChart`) → price
  history with analog markers.
- `GET /api/events/upcoming` (`UpcomingEventsBanner`) → non-numeric.

> Every headline probability on this page is §7 `PositiveOdds`; every "vs base
> rate" is §8/§9; every price band is §11.

### `/dashboard`
- `GET /api/regime/current` → market-health & indicators (descriptive, **not odds**).
- **`GET /api/assets/popular`** → per-flagship `Odds3M / Edge3M` (Part II).
- `GET /api/regime/matches` → analog list.

### `/` (landing)
- `GET /api/regime/current`, **`GET /api/assets/popular`**, and a featured
  **`GET /api/regime/odds/{symbol}`** — same numbers as their endpoints above.

### `/screener`
- **`GET /api/screener` / `/screener/full`** → the `Odds3M / BaseRate3M / Edge3M /
  Stance / Strength` table (Part II). Identical semantics to the per-asset 3M read.

### `/sectors`
- **`GET /api/sectors`** → `Odds3M / Edge3M / Stance / RelStrength3M` per SPDR
  sector vs SPY (Part II).

### `/watchlist` *(Pro)*
- **`GET /api/watchlist/overview`** (and per-symbol `GET /api/regime/odds/{symbol}`)
  → 3M summary per asset.

### `/macro`
- `GET /api/regime/current` → indicator detail (descriptive, **not odds**).

---

## Part IV — Validation summary

| # | Quantity | Formula (essence) | Source | Verdict |
|---|----------|-------------------|--------|---------|
| 1 | Snapshot / YoY / momentum | fwd-fill; `(x−x₋₁yr)/|x₋₁yr|·100`; `x'−x'₋₆mo` | `MacroSnapshotBuilder` | ✔ |
| 2 | z-score | `(x−mean)/std`, pop. std, visible-only | `BuildZScoreVector` | ✔ |
| 3 | Distance / similarity | `√(mean_f MSD_f)`; `S=100/(1+D)` | `SharedDimensionDistance` | ✔ |
| 4 | Analog select / rank | ≥6mo old, decluster 6mo, top 40; `rank/cand·100` | `FindMatches` | ✔ |
| 5 | Kernel weight | `exp(−(d/median_d)²)`, `d=100/S−1` | `KernelWeights` | ✔ ⚠ rounded S |
| 6 | Analog return | `(exit−entry)/entry·100` | `ComputeReturns` | ✔ |
| 7 | PositiveOdds | `base + 0.4(raw−base)` (raw if no base) | `ComputeOdds` | ✔ ⚠ post-shrink |
| 8 | BaseRate | share of N-day windows positive, ≥24 | `ComputeBaseRate` | ✔ ⚠ overlap |
| 9 | Edge | `PositiveOdds − base` `= 0.4(raw−base)` | `ComputeOdds` | ✔ |
| 10 | Avg / Median / Best / Worst | wtd mean; wtd P50; **unwtd** max/min | `ComputeOdds` | ✔ ⚠ best/worst unwtd |
| 11 | Price targets | `price·(1+P{25,50,75}/100)` | `WeightedPercentile` | ✔ ⚠ percentile≠band |
| 12 | Stance / Strength | edge≥5 & odds≥55 …; |edge| & sample | `TradeRead` | ✔ |
| 13 | MA200 breakdown | 200-close mean split | `ComputeBreakdown` | ✔ descriptive |
| 14 | RelStrength | `assetRet₉₀ − SPYRet₉₀` | `RelativeStrength` | ✔ |
| 15 | Directional accuracy | `#(pred≥50 ⇔ actual)/n` | `BacktestService` | ✔ |
| 16 | Brier | `mean((p−outcome)²)` | `BacktestService` | ✔ |
| 17 | Calibration | per-band pred vs actual | `BuildCalibration` | ✔ |

**Conclusion: no calculation errors found.** Every displayed odd, edge, base rate,
target and backtest metric matches its intended formula, and the shared-engine
design keeps them consistent across pages. The ⚠ items below are correct-but-worth-
knowing interpretation points, not bugs.

**Independent end-to-end reproduction (2026-07-22).** The full pipeline was
re-implemented from scratch in Python and diffed against live production output —
**every number matched exactly**:

- **Matching (§1–§4)** — from the raw macro rows (103k points, DB), the Python
  port rebuilt monthly snapshots, YoY/momentum transforms, z-score normalisation,
  family-weighted distance, similarity and declustered selection, and reproduced
  the engine's analog list for SPY: **40/40 analogs matched** — same dates *and*
  same similarity scores (to 0.1), in the same order (439 snapshots, 421
  candidates).
- **Odds math (§5–§11)** — from the raw daily candles plus that analog list, the
  Python port recomputed kernel weights → forward returns → raw odds → shrinkage →
  edge → weighted percentiles → price targets → base rate for SPY and GLD across
  all three horizons: **72/72 fields matched to the last decimal/cent** (odds,
  base rate, edge, average/median/best/worst return, P25/P50/P75 targets, counts).
- **Identity spot-checks** — SPY/BTC/GLD × 3 horizons: **45/45** (`Edge ==
  round(PositiveOdds − BaseRate)`, `PriceTargetMid == price·(1 + median/100)`,
  monotonic targets, `worst ≤ median ≤ best`, range/count bounds).

(One benign artifact: `Edge` is rounded from the *unrounded* shrunk odds and base
rate, so it can differ by 0.1 from `displayedOdds − displayedBaseRate` — e.g. GLD
3M shows odds 65.6, base 62.3, edge 3.4; both correct. Crypto assets use a
different analog profile that no endpoint exposes, so BTC's *matching* wasn't
independently reproduced — only its odds identities.)

---

## Part V — Caveats worth surfacing (all intentional)

1. **Displayed odds & edge are post-shrinkage.** `PositiveOdds` is pulled 60% of
   the way from the raw analog frequency toward the asset's base rate, and
   **`Edge` is exactly 40% of the raw analog edge**. This is the honesty
   mechanism (a few dozen analogs can't support extreme claims) — but a reader
   comparing the headline % to "positive analogs ÷ total" will see a gap.
2. **Thin-history assets show *raw* odds.** When there aren't ≥ 24 base-rate
   windows, `PositiveOdds` is the unshrunk raw odds and `Edge = 0`. The meaning of
   the headline number changes in this regime; `MatchesUsed` and the "thin
   evidence" TradeRead wording are the tells.
3. **Base-rate samples overlap.** The 21-candle stride is shorter than the horizon,
   so windows autocorrelate; the ≥ 24 floor is 24 *overlapping* windows, i.e. fewer
   independent observations than it looks. Point estimate unbiased; uncertainty
   understated.
4. **Best/Worst and Total/Positive *cases* are unweighted.** They describe the raw
   analog set; the headline odds/median/targets are kernel-weighted **and** shrunk.
   Don't expect `PositiveCases/TotalCases` to equal `PositiveOdds`.
5. **Price-target "Optimistic" (P75) is a percentile, not a floor/ceiling of gain.**
   In a bearish regime it can be below spot.
6. **Kernel weights use 1-dp-rounded similarity.** A negligible precision loss from
   the DTO rounding, noted for completeness.
7. **State- and cycle-conditioning are inactive in production** (null bandwidths).
   Only kernel weighting + fixed shrinkage shape the live odds.

---

*Generated 2026-07-22 from a full read of the odds engine
(`RegimeDeck.Application/Odds/*`, `…/Regime/MacroSnapshotBuilder.cs`,
`…/Common/CandleMath.cs`, `…/Sectors/RelativeStrength.cs`,
`…/Backtesting/BacktestService.cs`). Update this doc when any of those change.*
