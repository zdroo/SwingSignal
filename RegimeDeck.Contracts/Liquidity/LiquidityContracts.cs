namespace RegimeDeck.Contracts.Liquidity;

/// One liquidity gauge's current level and near-term direction, read through a
/// risk-asset lens (expanding liquidity = tailwind). Descriptive, not advice.
public record LiquidityReadingDto(
    string Name,
    decimal Value,
    string Unit,         // "$T" | "index"
    double ChangePct3M,
    string Trend,        // "Expanding" | "Contracting" | "Flat" | "Rising" | "Falling"
    string Tone,         // "good" | "bad" | "neutral"
    string Plain);       // one-line plain-words read

/// One central bank's contribution to global liquidity, converted to USD.
public record LiquidityComponentDto(
    string Name,
    decimal ValueUsdTrillions,
    double SharePct);

/// Aligned weekly point for the chart. Btc/Spy are the aligned closes (null
/// before the asset had data); liquidity levels are in USD trillions.
public record LiquidityPointDto(
    DateTime Date,
    decimal GlobalLiquidity,
    decimal FedNetLiquidity,
    decimal? Btc,
    decimal? Spy);

/// How closely an overlay asset has tracked global liquidity over the window.
public record LiquidityOverlayDto(
    string Symbol,
    int CorrelationPct,  // Pearson × 100 over the window
    int Months);

public record LiquidityDashboardDto(
    DateTime AsOf,
    LiquidityReadingDto GlobalLiquidity,
    LiquidityReadingDto FedNetLiquidity,
    List<LiquidityComponentDto> Components,
    LiquidityReadingDto UsM2,
    LiquidityReadingDto Dollar,
    List<LiquidityPointDto> Series,
    List<LiquidityOverlayDto> Overlays,
    string Note);
