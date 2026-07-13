using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Odds;

/// The three-month headline read distilled from an AssetOddsDto — the numbers
/// the screener, sector board and watchlist all surface. The nullable fields
/// are null when the asset has no computable odds yet (thin history); this is
/// the single place the "no cases → null" convention lives.
public readonly record struct ThreeMonthSummary(
    double? Odds3M,
    double? BaseRate3M,
    double? Edge3M,
    string? Stance,
    string? Strength)
{
    public static ThreeMonthSummary From(AssetOddsDto odds)
    {
        var threeMonths = odds.ThreeMonths;
        var hasOdds = threeMonths.TotalCases > 0;

        return new ThreeMonthSummary(
            Odds3M: hasOdds ? threeMonths.PositiveOdds : null,
            BaseRate3M: hasOdds ? threeMonths.BaseRate : null,
            Edge3M: hasOdds ? threeMonths.Edge : null,
            Stance: odds.TradeRead?.Stance,
            Strength: odds.TradeRead?.Strength);
    }
}
