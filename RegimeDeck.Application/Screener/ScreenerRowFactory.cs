using RegimeDeck.Application.Odds;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Screener;

/// Maps a computed AssetOddsDto onto a cacheable ScreenerRow. Pure so the
/// mapping is unit-tested without the DB or the timer.
public static class ScreenerRowFactory
{
    public static ScreenerRow Create(Asset asset, AssetOddsDto odds, DateTime computedAt)
    {
        var summary = ThreeMonthSummary.From(odds);

        return new ScreenerRow
        {
            Symbol = asset.Symbol,
            Name = asset.Name,
            MarketType = asset.MarketType,
            CurrentPrice = odds.CurrentPrice,
            Odds3M = summary.Odds3M,
            BaseRate3M = summary.BaseRate3M,
            Edge3M = summary.Edge3M,
            Stance = summary.Stance,
            Strength = summary.Strength,
            ComputedAt = computedAt,
        };
    }
}
