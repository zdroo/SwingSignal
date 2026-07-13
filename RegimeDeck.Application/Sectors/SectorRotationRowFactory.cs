using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Sectors;

/// Builds a cacheable SectorRotationRow from the ETF's computed odds plus its
/// relative strength. Pure, so the "no computable odds" case is tested without
/// the DB or the timer.
public static class SectorRotationRowFactory
{
    public static SectorRotationRow Create(
        string symbol, string sector, AssetOddsDto odds, double? relStrength, DateTime computedAt)
    {
        var threeMonths = odds.ThreeMonths;
        var hasOdds = threeMonths.TotalCases > 0;

        return new SectorRotationRow
        {
            Symbol = symbol,
            Sector = sector,
            Odds3M = hasOdds ? threeMonths.PositiveOdds : null,
            Edge3M = hasOdds ? threeMonths.Edge : null,
            Stance = odds.TradeRead?.Stance,
            RelStrength3M = relStrength,
            ComputedAt = computedAt,
        };
    }
}
