using SwingSignal.Domain.Enums;

namespace SwingSignal.Application.Regime;

// Algorithm feature toggles, used by the backtester to compare configurations.
// Production always runs with the validated configuration.
public record MatchingOptions(
    bool Decluster,
    bool SimilarityWeighting,
    bool FamilyWeighting,
    bool KernelAllHistory,
    bool ShrinkToBaseRate,
    double? StateBandwidth,          // null = no asset-state conditioning; smaller = stricter
    MacroIndicatorType[]? DimensionFilter = null,  // null = match on all dimensions
    double? CryptoCycleBandwidth = null, // null = no halving-phase/Mayer conditioning
    double? ShrinkagePrior = null,   // null = legacy fixed shrinkage; else adaptive k = nEff/(nEff+prior)
    bool FloorAnalogsToAssetHistory = false, // analogs only from months the asset traded
    int? BaseRateTrailingYears = null) // null = all-history base rate; else only the last N years
{
    // Asset-state conditioning is OFF in production: pre-2015 tuning showed gains
    // for QQQ/GLD, but they did not survive 2015+ validation (GLD reversed to a
    // loss). The plumbing stays for re-testing via the backtest stateH parameter.
    // Production filters to the macro-only fingerprint so the crypto-native
    // snapshot dimensions can never leak into stock/ETF/forex matching.
    public static readonly MatchingOptions Production =
        new(true, true, true, true, true, null, MacroSnapshotBuilder.MacroDimensions);
    public static readonly MatchingOptions Baseline = new(false, false, false, false, false, null);

    // Crypto assets match on the liquidity/risk-appetite subset only: US labor,
    // housing and commodity cycles added noise for BTC/ETH (walk-forward
    // validated — see CryptoDimensions).
    // Crypto-cycle conditioning (halving phase + Mayer multiple) stays OFF:
    // tested July 2026 at h ∈ {0.5..1.5}, both pre-2022 and 2022+ — Brier
    // moved ±0.004 with no consistent direction. The plumbing stays for
    // re-testing via the backtest cycleH parameter.
    public static readonly MatchingOptions CryptoProduction = Production with
    {
        DimensionFilter = MacroSnapshotBuilder.CryptoDimensions,
        FloorAnalogsToAssetHistory = true,
    };

    // Crypto profile + on-chain cycle gauges. Production for SHORT horizons
    // only: walk-forward showed the gauges sharpen short-horizon matching
    // (BTC 30d 0.230→0.224) and make long-horizon analogs overconfident
    // (180d 0.250→0.259). Re-testable at any horizon via the backtest
    // profile "crypto-native".
    public static readonly MatchingOptions CryptoShortHorizon = CryptoProduction with
    {
        DimensionFilter = MacroSnapshotBuilder.CryptoDimensionsWithNatives,
    };

    // Horizon boundary between the crypto profiles. Declared a priori as the
    // midpoint between the validated cells (30d wants cycle gauges, 90d/180d
    // want macro-only depth) — deliberately NOT tuned.
    public const int ShortHorizonMaxDays = 45;

    public static MatchingOptions ForMarket(MarketType marketType, int horizonDays) =>
        marketType != MarketType.Crypto ? Production
            : horizonDays <= ShortHorizonMaxDays ? CryptoShortHorizon
            : CryptoProduction;

    // How many declustered analog periods feed the odds when KernelAllHistory is on.
    // High enough to cover ~3 decades at 6-month spacing.
    public const int AnalogCount = 40;

    // Fixed shrinkage of raw analog odds toward the asset's base rate
    // (0 = pure base rate, 1 = raw analog odds). Adaptive evidence-scaled
    // shrinkage (k = nEff/(nEff+M), OddsMath) was tested July 2026 at
    // M ∈ {10..50}: flat-to-worse in BOTH tuning (pre-2022) and validation
    // (2022+) — BTC 30d Brier degraded 0.221→0.234 because over-shrinking
    // destroyed a real short-horizon signal. OFF in production; re-test via
    // the backtest shrinkM parameter.
    public const double Shrinkage = 0.4;

    public string Label => this == Production ? "current"
        : this == Baseline ? "baseline"
        : this == CryptoProduction ? "crypto"
        : this == CryptoShortHorizon ? "crypto-short"
        : "custom";
}
