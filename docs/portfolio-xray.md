# Portfolio Macro X-Ray — Spec

**North star:** a *mirror*, not a crystal ball. The user enters holdings; we show
their true macro exposure, concentration, and how a book like theirs behaved in
regimes like today — framed as risk and context, never "buy/sell this."

## Decisions (locked)
- **Input:** symbol + dollar value → weights computed server-side.
- **Access:** free with an account (Pro later gates saved portfolios + factor tilts).
- **Storage:** stateless — holdings are POSTed, analyzed, never persisted.

## The core idea
The regime matcher finds ~40 analog months for today's macro. For the portfolio
view we use **one shared macro analog set** (the equity/`Production` profile) across
*every* holding — including crypto — so that for each analog month we can compute
every holding's forward return and **weight-sum into a portfolio return per month**.
The distribution across analog months is "how a book like yours fared after regimes
like today," and because it's date-aligned it captures **diversification** (offsetting
holdings net out) instead of naively summing per-asset worst cases.

> Crypto's dedicated odds page adds crypto-cycle conditioning; the X-ray is a
> *macro* lens, so it deliberately uses the macro analog set for the whole book.

Analog months are only counted when **all** holdings have candle data, so every
month reflects the full portfolio; the count `N` drives the confidence chip (a young
crypto holding shrinks `N` → lower confidence, surfaced honestly).

## What it computes
**Per holding:** weight, asset class (risk-on/defensive), 3-month median & worst
analog outcome, realized volatility, liquidity sensitivity (YoY-return correlation
to global-liquidity YoY growth — reuses `LiquidityMath.Correlation`).

**Portfolio:**
- **Regime posture** — weighted median analog outcome + the portfolio distribution.
- **Concentration** — top holding %, top-3 %, Herfindahl (HHI), by asset class.
- **Exposure** — risk-on vs defensive split, weighted liquidity sensitivity.
- **Regime outcome** — date-aligned distribution: median / worst / best / % positive
  over 3M, `N` analogs, confidence.
- **Plain-words reads** — honest takeaways, never advice.

## Deliberately NOT
No buy/sell, no "optimal portfolio," no forecast of the user's own future value, no
fabricated precision — sample size and range always shown.

## Reuse
`IMacroRegimeService` (shared analog months) · `ICandleRepository` + `CandleMath`
(per-analog returns, volatility) · `MacroSnapshotBuilder.KernelWeights` · `MatchingOptions`
· `ILiquidityService` (global-liquidity series) + `LiquidityMath.Correlation` ·
`MarketType` for classification · `IAssetIngestionService.EnsureSupportedAsync` (ingest
unknown symbols) · FE: `AssetSearch`, `ConfidenceRisk`, dataviz palette.

## Shape
- **API:** `POST /api/portfolio/xray` `{ holdings: [{ symbol, value }] }` →
  `PortfolioXrayDto`. Auth-gated, rate-limited (compute).
- **FE:** `/portfolio` — holdings input, exposure bars, concentration meter, the
  portfolio regime-outcome headline (ConfidenceRisk framing), per-holding table,
  reads.

## Phase 2 (later)
Per-asset rate/dollar factor tilts, saved portfolios (auth + table), position-sizing
suggestions.
