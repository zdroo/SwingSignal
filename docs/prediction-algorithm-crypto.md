# How SwingSignal Computes Odds — Crypto (BTC, ETH, USDT pairs)

This document traces a crypto prediction from the HTTP request to the numbers
on the asset page, at full detail. The pipeline shares its skeleton with the
stock pipeline (`prediction-algorithm-stocks.md`) but differs in **two
walk-forward-validated ways**: crypto matches on a **liquidity-focused subset
of the macro fingerprint**, and its analog candidates are **restricted to
months the asset actually traded**. Both changes shipped July 2026 after
before/after backtesting; the numbers are at the end.

The core idea is unchanged: **find the months in history whose macro
conditions most resembled today's, look at what the coin actually did after
each of those months, and report that distribution honestly.**

---

## 1. The endpoints

Same two endpoints as every asset, in `RegimeController`:

| Endpoint | Returns | Access |
|---|---|---|
| `GET /api/regime/odds/{symbol}` | `AssetOddsDto` — odds for 1M / 3M / 6M plus explanations, price targets, analog breakdown | `BTCUSDT` is a flagship symbol (anonymous); other coins need a free account |
| `GET /api/regime/odds/{symbol}/period?days=N` | `AssetPeriodOddsDto` — one custom window, 7–365 days | Account required |

Controller steps before the math:

1. **Symbol normalization**: `BTC`, `BTC/USD`, `BTCUSD` → `BTCUSDT`
   (likewise ETH/SOL/BNB). Crypto is detected by the `USDT`/`BUSD` suffix.
2. **Gate check** + best-effort search logging.
3. **On-demand ingestion**: first request for a coin registers it and pulls
   its full daily history from **Binance** — paginated (1000 candles per
   page), requesting up to 10 years. In practice Binance history *starts at
   the pair's listing*: **BTCUSDT and ETHUSDT begin 2017-08-17 (~8.9 years,
   ~3,250 daily candles)**; younger coins have less. This hard data floor
   drives both crypto-specific design decisions below.

Everything after that is `HistoricalOddsService.LoadContextAsync`, which sees
`asset.MarketType == Crypto` and selects
`MatchingOptions.CryptoProduction` via `MatchingOptions.ForMarket`.

---

## 2. Crypto difference #1 — the liquidity-focused fingerprint

The full fingerprint has 31 dimensions across 7 families (see the stocks
doc). For crypto, matching uses only the **16 dimensions**
(`MacroSnapshotBuilder.CryptoDimensions`) that plausibly drive a
non-cash-flow, liquidity-sensitive asset:

| Family | Dimensions kept |
|---|---|
| Policy & liquidity | Fed funds rate (+6M momentum), 10Y real yield, Fed balance sheet (YoY), M2 (YoY), reverse repo |
| Long rates | 10Y Treasury yield (+6M momentum) |
| Inflation (drives the Fed path) | CPI (+6M momentum), Core PCE |
| Market stress & risk appetite | VIX, high-yield spread (+6M momentum), dollar index, Crypto Fear & Greed |

**Dropped**: the entire labor family (unemployment, jobless claims, Sahm),
growth & consumer (GDP, retail sales, housing starts, sentiment), and
commodities (gold, oil, copper). Bitcoin does not care about housing starts;
including them made the distance metric reward the wrong kind of
"similarity". Because the subset spans only 4 families, the distance
validity minimums relax accordingly (≥8 shared dimensions — half the filter —
and ≥3 families, vs 8/5 for the full set).

Everything else about matching is identical to stocks: monthly snapshots
since 1990, YoY transforms, z-score normalization, family-weighted RMS
distance, `similarity = 100/(1+d)`, 6-month recency cutoff, greedy 6-month
declustering.

## 3. Crypto difference #2 — the history floor

For stocks, analog candidates come from all months since ~1990. For crypto,
candidates are **restricted to months on or after the asset's first candle**
(`FindMatches minCandidateDate = candles[0].OpenTime`).

Why: an analog from 1995 can never be scored for BTC — there is no BTC price
to measure an outcome from. Without the floor, up to half of BTC's 40
analogs were unscoreable dead weight; the effective sample behind the odds
was ~8–20 episodes. With the floor, every selected analog is scoreable.

The arithmetic consequence: BTC's candidate pool is ~96 months (2017-08 →
six months ago), and 6-month declustering caps the selection at **~15
analogs instead of 40** — but all 15 produce a measurable return, and the
page's "Based on N historical macro periods" reflects that honestly.

---

## 4. Weighting, outcomes, and odds — shared machinery

Identical to stocks, summarized here for completeness:

- **Gaussian kernel weights**: `d = 100/similarity − 1`,
  `weight = exp(−(d/median d)²)` — nearest analogs dominate smoothly.
- **Outcome measurement**: entry = candle nearest the analog date (±7 days);
  exit = candle nearest `date + N days` (tolerance `clamp(N/3, 2, 7)` days);
  return = percent change. Crypto trades 24/7, so candle gaps are rare and
  nearly every analog scores exactly.
- **Raw odds** = weighted share of positive returns.
- **Base rate** = share of ALL rolling N-day windows in the coin's history
  that ended positive (21-trading-day stride, ≥24 windows required). For BTC
  at 90 days this is ≈52% — unlike SPY's ≈70%, crypto's base rate hovers
  near a coin flip, so the regime edge matters relatively more.
- **Shrinkage**: `odds = base + 0.4 × (raw − base)` — a handful of episodes
  never justifies extreme claims.
- **Price targets** from weighted P25/P50/P75 of the analog returns; note
  that crypto's ±30% quarters make these ranges *wide* — a wide range is the
  honest answer, not a bug.
- **MA200 analog breakdown** (descriptive only) and explanation bullets.

**Crypto-specific explanation bullets** (`CryptoCycle`): the halving-cycle
position ("N months past the last halving — historically the markup
window / late-cycle / pre-halving consolidation") and the **Mayer Multiple**
(price ÷ its own 200-day average; >2.4 historically overheated, <0.8
historically depressed). These are *context only* — see §6.

---

## 5. Validation — what shipped and why

Both crypto changes were accepted only after the walk-forward backtest
(same code path, expanding-window, no lookahead) showed improvement over the
previous production config, per horizon (Brier score / directional
accuracy — lower Brier is better, 0.250 = coin-flip):

| Asset · horizon | Old (full fingerprint, no floor) | Shipped (crypto profile + floor) |
|---|---|---|
| BTC 30d | 0.223 / 69% | **0.220 / 72%** |
| BTC 90d | 0.250 / 56% | **0.247 / 56%** |
| BTC 180d | 0.273 / 45% | **0.263 / 58%** |
| ETH 30d | 0.241 / 57% | **0.239 / 61%** |
| ETH 90d | 0.249 / 49% | 0.251 / **58%** |
| ETH 180d | 0.279 / 38% | **0.274 / 42%** |

The headline win: BTC's 6-month direction went from worse-than-a-coin-flip
(45%) to 58%. SPY was verified unchanged (its auto profile is the full set).
The backtest endpoint keeps `profile=crypto|default` and `floorHistory=`
parameters so any future change must beat this table before shipping.

## 6. What was built, tested, and deliberately turned OFF

**Halving-phase + Mayer-multiple conditioning** (`CryptoCycle.CycleFactor`):
a Gaussian factor that down-weights analogs from a different point in the
~4-year halving cycle (circular phase distance) or a different Mayer state
(log-scale gap). Fully wired behind `MatchingOptions.CryptoCycleBandwidth`
and the backtest `cycleH` parameter.

Verdict: at every bandwidth tested (0.5–1.5), both pre-2022 (tuning) and
2022+ (validation), Brier moved ±0.004 with no consistent direction — noise.
With only ~2 full crypto cycles of data, "where we are in the cycle"
describes the past without predicting the future. **Production keeps it
off**; the halving/Mayer bullets remain as context because they're useful
framing even without predictive power. This mirrors the asset-state
conditioning story: we keep the plumbing, we don't ship what validation
rejects.

---

## 7. Honest limitations

- **Short history.** ~59–71 backtest points per horizon vs SPY's 319, from a
  single 8.9-year window covering roughly two crypto cycles. Statistical
  noise is large; treat all crypto odds as lower-confidence than equity odds.
- **Macro-only drivers.** The fingerprint cannot see crypto-native shocks —
  exchange collapses, leverage flushes, ETF flows. Macro twins are not
  crypto twins; this is the main residual error source.
- **Volatility.** Even correct analogs produce a huge outcome spread;
  medians from ~15 weighted episodes are unstable. The wide Conservative ↔
  Optimistic range on crypto pages is the distribution telling the truth.
- **Coverage.** Any Binance USDT pair works (younger listings = fewer
  analogs = humbler numbers). BTC dominance and total crypto market cap are
  *not* supported — no free historical source identified yet.

## 8. Worked micro-example (real values, July 2026)

`GET /api/regime/odds/BTCUSDT`, 3-month horizon:

1. Crypto profile → 16-dimension matching, candidates floored at 2017-08.
2. 15 declustered analogs selected (earliest: Dec 2017), all scoreable.
3. Kernel-weighted positive share of the 15 returns: raw ≈ 38%.
4. BTC 90-day base rate over its full history: 52%.
5. Published odds: 52 + 0.4 × (38 − 52) = **43.2%**, edge **−8.8pp** — the
   current regime historically *worsened* BTC's coin-flip base rate.
6. Breakdown: 8 analogs had BTC above its 200-day average at the time, 6
   below, shown on the price chart as blue/amber dots — context, not signal.
