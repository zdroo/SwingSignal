# How SwingSignal Computes Odds — Stocks, ETFs, Forex & Commodities

This document traces a prediction from the HTTP request to the numbers on the
asset page, at full detail, for every non-crypto asset (stocks, ETFs, indices,
forex pairs, commodity futures). The crypto pipeline shares this skeleton but
differs in two validated ways — see `prediction-algorithm-crypto.md`.

The core idea in one sentence: **we never predict prices — we find the months
in history whose macro conditions most resembled today's, look at what the
asset actually did in the weeks after each of those months, and report that
distribution honestly.**

---

## 1. The endpoints

Two endpoints produce predictions, both in `RegimeController`:

| Endpoint | Returns | Access |
|---|---|---|
| `GET /api/regime/odds/{symbol}` | `AssetOddsDto` — odds for 1M / 3M / 6M plus explanations, price targets, and the analog breakdown | Anonymous for the flagship symbols (`BTCUSDT`, `SPY`, `GC=F`); any other symbol requires a free account |
| `GET /api/regime/odds/{symbol}/period?days=N` | `AssetPeriodOddsDto` — odds for one custom window, 7–365 days | Account required regardless of symbol |

What the controller does before any math:

1. **Symbol normalization** (`SymbolNormalizer.Normalize`): trims, resolves
   aliases (`S&P500` → `SPY`, `GOLD` → `GC=F`, `EUR/USD` → `EURUSD=X`),
   uppercases (invariant culture).
2. **Gate check** and best-effort **search logging** (`SearchLogService` —
   a logging failure can never fail the request).
3. **On-demand ingestion** (`OnDemandIngestionService.EnsureIngestedAsync`):
   if the symbol has never been requested, it is registered (market type
   detected from the suffix: `=X` forex, `=F` commodity, else stock) and its
   full daily candle history is fetched from **Yahoo Finance — up to 30
   years**. If Yahoo returns nothing, the endpoint answers 503.

From there everything happens in `HistoricalOddsService`. Both endpoints load
the same `OddsContext` (asset, candles, matched analogs, analog weights); the
fixed-horizon endpoint computes it for 30/90/180 days, the custom endpoint for
your `days`.

---

## 2. The raw material: 31 macro dimensions since 1990

The regime "fingerprint" is built from **26 stored indicators** plus **5
derived momentum dimensions**, refreshed daily by background ingestion:

| Family | Indicators | Source |
|---|---|---|
| 0 — Policy & liquidity | Fed funds rate, 10Y real yield, Fed balance sheet, M2 money supply, reverse repo | FRED (DBnomics fallback) |
| 1 — Rates & curve | 10Y, 2Y, 3M Treasury yields; 10Y−2Y and 10Y−3M spreads | FRED |
| 2 — Inflation | CPI, Core PCE | FRED |
| 3 — Labor | Unemployment, jobless claims, Sahm rule | FRED |
| 4 — Growth & consumer | GDP, retail sales, housing starts, consumer sentiment | FRED |
| 5 — Market stress & risk appetite | VIX, high-yield spread, dollar index, Crypto Fear & Greed | Yahoo / FRED / Alternative.me |
| 6 — Commodities | Gold, WTI oil, copper | Yahoo / FRED |

The five **momentum dimensions** are 6-month changes of Fed funds,
unemployment, 10Y yield, high-yield spread, and CPI (computed on the
transformed series). They let the matcher distinguish "5% Fed funds on the way
up" from "5% on the way down" — the same level, radically different regimes.

**Monthly snapshots** (`MacroSnapshotBuilder.BuildAllAsync`, cached 1 hour):

- Each indicator becomes a forward-filled monthly series (a month's value is
  the last observation on or before month-end), starting from the earliest
  data (~1990).
- **YoY transform**: trending level series (CPI, GDP, gold, oil, balance
  sheet, M2, Core PCE, retail sales, housing starts, copper) are stored as
  percent change vs the same month a year earlier. A raw CPI z-score would
  just measure "how recent is this month"; the meaningful signal is the
  inflation *rate*.
- A month becomes a snapshot only if it has **at least 8 indicators** —
  early-1990s months with sparse data still qualify, so the matcher can see
  three decades of regimes.

---

## 3. Finding the analogs (`MacroSnapshotBuilder.FindMatches`)

Given the snapshots and "today" (the latest snapshot), the matcher selects the
**40 most similar historical months** (`MatchingOptions.AnalogCount`):

1. **Z-score normalization.** Every dimension of every snapshot is converted
   to "standard deviations from its own historical mean" using stats computed
   over all visible history. This puts Fed funds (percent) and the balance
   sheet (trillions, as YoY%) on the same scale.

2. **Candidate filter.** A candidate month must be at least **6 months older
   than today** — otherwise "the most similar month" is trivially last month.

3. **Family-weighted distance.** For each candidate we take the dimensions it
   shares with today (minimum **8 shared dimensions across ≥5 families**,
   otherwise the candidate is skipped as incomparable) and compute an RMS
   distance where **each of the 7 families contributes equally**. Without
   this, the five correlated rate series would outvote the entire labor
   market 5-to-1.

4. **Similarity score.** `similarity = 100 / (1 + distance)`. Exact repeats
   never happen; the best match in 30+ years typically scores ~60–65, and
   anything above ~55 is unusually strong.

5. **Declustering.** Candidates are picked greedily best-first, skipping any
   month within **6 months of an already-picked one**. Late-2008 was one
   crisis, not five independent data points — without declustering a single
   dramatic era would fill the analog list. Each pick keeps its rank against
   the *full* candidate list, which is what the "top 2%" chips on the
   dashboard mean.

For a stock, the candidate pool is every qualifying month since ~1990, so the
40 analogs typically span the early-90s expansion, the dot-com era, 2008, the
2010s QE decade, the 2020 crash, and the 2022 hiking cycle.

**What is deliberately NOT in the fingerprint: the asset's own chart.** The
matching is macro-only. We built asset-state conditioning (RSI, distance from
MA200, 52-week-high proximity re-weighting) and it improved in-sample results
but **failed walk-forward validation on 2015+ data** (GLD reversed to a
loss), so it is off in production (`MatchingOptions.StateBandwidth = null`).
The UI shows the MA200 split of analogs as *descriptive context* instead.

---

## 4. Weighting the analogs (Gaussian kernel)

Forty analogs are not equally relevant. Each similarity is converted back to a
distance (`d = 100/similarity − 1`) and given a Gaussian weight:

```
weight = exp( −(d / bandwidth)² ),   bandwidth = median distance of the 40
```

The closest analogs dominate; the fortieth fades smoothly toward zero rather
than being either fully-in or cut off. Using the *median* distance as
bandwidth makes the weighting self-calibrating: in a "nothing looks like
today" regime all weights flatten, in a "textbook repeat" regime the top
handful dominates.

---

## 5. Measuring what actually happened

For each weighted analog date and each horizon N (30/90/180 or your custom
`days`), from the asset's daily candles:

- **Entry**: the candle nearest the analog month (binary search, tolerance
  ±7 days). No candle in the window → the analog is skipped for this asset.
- **Exit**: the candle nearest `analog date + N days`, with a tolerance of
  `clamp(N/3, 2, 7)` days — tight for short horizons so a 7-day prediction
  can't accidentally match an exit candle sitting next to the entry.
- **Return**: `(exit − entry) / entry × 100`.

Analogs that predate the asset's price history simply can't be scored and are
skipped (the price chart discloses the count). SPY has ~30 years of Yahoo
history, so nearly all 40 analogs score.

---

## 6. From returns to odds (`ComputeOdds`)

Let `w_i` be the kernel weights and `r_i` the returns of the scoreable
analogs.

**Raw analog odds** — the weighted share of positive outcomes:

```
raw = Σ(w_i where r_i > 0) / Σ(w_i) × 100
```

**Base rate** — computed from the asset's *entire* history, ignoring macro:
rolling N-day windows sampled every 21 trading days; the base rate is the
percentage that ended positive (requires ≥24 windows, else null). For SPY at
90 days this is ≈70% — stocks mostly go up; any honest 3-month prediction
must start from that fact.

**Shrinkage** — the published odds are pulled toward the base rate:

```
odds = base + 0.4 × (raw − base)        (MatchingOptions.Shrinkage = 0.4)
```

A few dozen correlated historical episodes cannot justify extreme probability
claims. If the analogs say 85% and the base rate is 70%, we publish 76%. The
**edge** (`odds − base`) is what the macro regime actually contributes, which
is why the UI displays it next to every number.

**The rest of the response**, all from the same weighted return
distribution:

- **Avg return** — weighted mean (outlier-sensitive by design; compare with
  the median).
- **Median / P25 / P75** — *weighted* percentiles: walk the sorted returns
  accumulating weight until 25/50/75% of total weight is passed.
- **Price targets** — `current price × (1 + percentile/100)`: Conservative
  (P25), Base Case (P50), Optimistic (P75). One-in-four historical cases
  ended below Conservative and one-in-four above Optimistic — they frame the
  typical range, not the extremes.
- **Best / Worst case** — the single best and worst analog outcomes,
  unweighted extremes shown as honest bounds.

The 1M/3M/6M endpoint additionally returns the **MA200 breakdown**
(descriptive analog split shown on the chart) and **explanation bullets**
(`MacroExplainerService`) that verbalize which indicators drive today's
regime.

---

## 7. Why you can trust (and how much to trust) these numbers

The exact same matching code runs in the **walk-forward backtest**
(`GET /api/backtest/{symbol}` — `BacktestService` calls the same
`FindMatches`, kernel, and shrinkage): for every month since data begins
(after a 60-month warm-up), it reproduces what would have been predicted
using *only data available then* — normalization stats, candidate lists and
base rates are all expanding-window, no lookahead — and scores it against
what happened.

Current validated picture (July 2026): **SPY 90d: Brier 0.229 over 319
predictions vs 0.269 for a naive base-rate predictor; predicted 67% vs actual
67% — well calibrated.** Longer horizons and most other assets sit closer to
the no-skill line: the honest summary is that macro matching gives
base-rate-quality odds with a small, real edge at shorter horizons. Every
number on the asset page is reproducible by the user through the backtest
panel — that reproducibility is the product.

---

## 8. Worked micro-example

Suppose today's macro fingerprint has its closest analogs in Mar 1995,
Nov 2006, Jun 2019, Oct 1998 … (40 total, declustered). For SPY at 90 days:

1. 38 of the 40 analogs have SPY candles → 38 returns, e.g. +6.2%, +3.1%,
   −8.4%, +11.0% …
2. Kernel weights make Mar 1995 (similarity 61) count ~3× more than the
   40th analog (similarity 44).
3. Weighted positive share: raw = 76%.
4. Base rate over all SPY history at 90d: 70%.
5. Published odds: 70 + 0.4 × (76 − 70) = **72.4%**, edge **+2.4pp**.
6. Weighted percentiles: P25 = −2.1%, P50 = +3.4%, P75 = +7.9% → price
   targets at current price × 0.979 / 1.034 / 1.079.
