# Crypto Accuracy Roadmap

Where the remaining crypto accuracy is, ranked by expected value. Ground
rules that produced every win so far: **new information beats new math**
(both shipped improvements changed the matching *inputs*; all three rejected
ones changed the *formula*), and **nothing ships without walk-forward
validation** (tune/validate split where a knob is tuned).

Current state (July 2026, Brier / directional accuracy, 2022+ eval):
BTC 30d 0.230/65% · 90d 0.242/54% · 180d 0.250/59%; ETH 90d 0.255/56%.
Coin flip = 0.250. Known calibration gap: ETH long-horizon bullish lean
(predicts ~59%, reality ~53%).

---

## Phase A — crypto-native matching dimensions (TESTED July 2026 — validation-neutral, NOT shipped)

**Outcome:** built end-to-end (ingestion + snapshot family 7 + extended
profile) and walk-forward tested. Net Brier across all ten asset/horizon
cells: **+0.001 (exactly neutral)** — BTC 30d clearly better (0.241→0.230
full-range, 0.230→0.224 on 2022+), BTC 180d clearly worse (0.250→0.259 on
2022+), direction accuracy +11pp net, SPY bit-for-bit unchanged. The cycle
gauges sharpen short-horizon matching but make long-horizon analogs
overconfident. Per protocol a wash doesn't ship as a blanket change — but the split
verdict is exactly what motivated Phase C, which then shipped the native
gauges for **short horizons only** (see below). The extended profile also
stays re-testable at any horizon via backtest `profile=crypto-native`.

**MVRV footnote:** bitcoin-data.com turned out to serve only a rolling
~4-year window, so MVRV is ingested (2022+) but not matchable — the Mayer
Multiple (BTC/200DMA from our own candles, 2014+) stands in as the
cycle-stretch gauge. Promote MVRV if a deep free source appears.

The idea: a "crypto-native" indicator family in the crypto matching
profile — market-wide cycle gauges the macro fingerprint cannot see. What
was built (all ingesting on the 12h cycle):

| Dimension | Meaning | Source (verified) | History |
|---|---|---|---|
| Mayer Multiple | BTC ÷ its 200-day average — cycle stretch (MVRV stand-in) | our own candles | 2014+ |
| Miner Puell | daily miner revenue ÷ its 365d average — miner-economy stress, documented top/bottom marker | blockchain.info charts (free) | 2013+ |
| Hash rate (YoY) | network security investment trend | blockchain.info charts (free) | 2013+ |
| Stablecoin supply (YoY) | USDT+USDC market cap — crypto's native money printer, the M2 of crypto | CoinMetrics community (free) | 2014+/2018+ |
| ETH/BTC ratio (YoY) | crypto-internal risk appetite | our own candles | 2017+ |
| MVRV | market cap ÷ realized cap | bitcoin-data.com — **rolling 4y window only** | 2022+ (unmatchable) |

All are BTC-market-wide gauges (crypto trades as one liquidity block), used
for every crypto asset's matching, invisible to stock matching. On-chain
data is immutable → walk-forward safe by construction.

**Availability notes from source vetting:** CryptoCompare now requires a
paid key (2024 CoinDesk change). CoinMetrics community lost RealCap/RevUSD/
HODL metrics — only price/mcap/supply remain free. blockchain.info dropped
its `mvrv` chart but kept miner metrics. bitcoin-data.com is the one free
MVRV source; if it dies, the fallback is Mayer Multiple (price/200DMA) from
our own candles as a weaker cycle gauge.

## Phase B — trailing-window base rate (TESTED July 2026 — REJECTED)

Hypothesis: the all-history base rate over-weights crypto's mostly-bull
past; a trailing window would drop ETH's predicted ~59% toward the observed
~53%. Tested with an a-priori 5-year window (no sweep): **flat-to-worse
everywhere** (ETH 180d Brier 0.276→0.282, BTC ±0.002), and the mechanism
was backwards — the last five years are *more* bull-tilted than full
history including the 2014-15/2018 winters, so the trailing base rate made
predictions MORE bullish. Conclusion: the ETH lean lives in the analog
outcomes, not the base rate. Plumbing kept (`baseRateYears` backtest
param); don't re-propose without a different mechanism for the lean.

## Phase C — horizon-split profiles (SHIPPED July 2026)

The week produced two mirrored trade-offs: the 2014+ backfill helped 180d
and hurt 30d; the crypto-native gauges helped 30d and hurt 180d. One shared
config forced every horizon to average those away. Now
`MatchingOptions.ForMarket(marketType, horizonDays)` splits crypto at an
**a-priori 45-day boundary** (midpoint between the validated 30d and 90d
cells — deliberately not tuned): horizons ≤45d use the crypto profile
*plus* the native cycle gauges; longer horizons stay macro-only with full
2014+ analog depth. Stocks unchanged at every horizon.

Validation (2022+, walk-forward): BTC 30d **0.224 / 67%**, 90d 0.242, 180d
**0.250 / 59%** — the best previously-measured value at every horizon
simultaneously, no interaction effects; ETH 30d improved to 0.235/61%
full-range; SPY bit-for-bit unchanged. This retroactively banks both
Phase A's short-horizon gain and the backfill's long-horizon gain.

## Phase D — funding rates (short-horizon leverage gauge)

Binance perpetual funding history (free, 2019+). The best documented
*short-horizon* signal (extreme positive funding = over-leveraged local
top), relevant exactly at 30d where the engine is strongest. History is
marginal (~7y) — revisit yearly as it grows.

## Continuous — time and coverage

Every month adds a validation point and a candidate month. And at some
point the highest-value move is not more Brier: run the backtest across
15-20 assets and publish per-asset confidence tiers — converting accuracy
limits into product honesty.

---

## Evaluated and rejected (do not re-propose without new data)

| Idea | Verdict | Why |
|---|---|---|
| Fibonacci retracements | rejected without testing | Price-derived (the family that failed validation), no documented predictive power at our monthly resolution, and brand-incompatible with "honest odds" |
| Saylor/corporate holdings, ETF flows | good instinct, unusable data | 2020+/2024+ only — one regime, unvalidatable. The rigorous version of "who is accumulating" is on-chain HODL/supply data (currently paywalled everywhere; revisit) |
| Asset-state conditioning (RSI/MA200/52w) | built, failed validation | In-sample gains reversed on 2015+ holdout (GLD to a loss). Plumbing kept behind backtest `stateH` |
| Halving-phase + Mayer conditioning | built, failed validation | ±0.004 Brier noise at every bandwidth, both windows. Plumbing kept behind `cycleH`; bullets remain as context |
| Evidence-scaled adaptive shrinkage | built, failed validation | Over-shrinking destroyed BTC's real 30d signal (0.221→0.234). Plumbing kept behind `shrinkM` |
| ETH pre-2017 history backfill | built, excluded by rule | Infancy hyper-growth ($1→$300) made 2022+ odds worse; maturity floor (first traded + 2y) excludes it |
| BTC dominance / total crypto mcap as symbols | blocked on data | No free historical source found (CoinGecko 365d cap, CoinMetrics paywalled) |
