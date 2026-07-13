using RegimeDeck.Application.Odds;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Sectors;

/// Builds a cacheable SectorRotationRow from the ETF's computed odds plus its
/// relative strength. Pure, so the mapping is tested without the DB or timer.
public static class SectorRotationRowFactory
{
    public static SectorRotationRow Create(
        string symbol, string sector, AssetOddsDto odds, double? relStrength, DateTime computedAt)
    {
        var summary = ThreeMonthSummary.From(odds);

        return new SectorRotationRow
        {
            Symbol = symbol,
            Sector = sector,
            Odds3M = summary.Odds3M,
            Edge3M = summary.Edge3M,
            Stance = summary.Stance,
            RelStrength3M = relStrength,
            ComputedAt = computedAt,
        };
    }
}
