namespace RegimeDeck.Contracts.Portfolio;

// ── Request ──────────────────────────────────────────────────────────────
public record PortfolioHoldingInput(string Symbol, decimal Value);

public record PortfolioXrayRequest(List<PortfolioHoldingInput> Holdings);

// ── Response ─────────────────────────────────────────────────────────────
public record HoldingXrayDto(
    string Symbol,
    string Name,
    string AssetClass,       // Stocks | Crypto | Gold | Bonds | Commodity | Forex
    string Posture,          // "Risk-on" | "Defensive"
    double WeightPct,
    decimal MedianReturn3M,  // median 3-month outcome across the shared analog months
    decimal WorstReturn3M,   // worst analog outcome — the realistic downside
    double? VolatilityPct,   // annualized realized volatility
    int? LiquidityBeta);     // correlation ×100 of YoY return to global-liquidity YoY growth

public record AssetClassWeightDto(string AssetClass, double WeightPct);

public record ConcentrationDto(
    double TopWeightPct,
    double Top3Pct,
    int Hhi,                 // Herfindahl index, 0–10000
    string Label,            // "Diversified" | "Moderate" | "Concentrated"
    List<AssetClassWeightDto> ByClass);

public record ExposureDto(
    double RiskOnPct,
    double DefensivePct,
    int LiquidityBeta,       // weighted correlation ×100
    string LiquidityLabel);  // "Liquidity-driven" | "Mixed" | "Liquidity-insulated"

/// The honest headline: how a book like this fared over 3 months after the
/// analog months for today's macro — date-aligned, so diversification counts.
public record PortfolioOutcomeDto(
    int Analogs,             // analog months where every holding had data
    string Confidence,       // "Very low" | "Low" | "Modest"
    double PositiveOddsPct,
    decimal MedianReturn,
    decimal WorstReturn,
    decimal BestReturn);

public record PortfolioXrayDto(
    string RegimeSummary,
    List<HoldingXrayDto> Holdings,
    ConcentrationDto Concentration,
    ExposureDto Exposure,
    PortfolioOutcomeDto Outcome,
    List<string> Reads,
    string Note);
