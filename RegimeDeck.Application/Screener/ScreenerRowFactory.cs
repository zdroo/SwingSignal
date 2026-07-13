using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Screener;

/// Maps a computed AssetOddsDto onto a cacheable ScreenerRow. Pure so the
/// mapping (including the "no computable odds" case) is unit-tested without
/// the DB or the timer.
public static class ScreenerRowFactory
{
    public static ScreenerRow Create(Asset asset, AssetOddsDto odds, DateTime computedAt)
    {
        var threeMonths = odds.ThreeMonths;
        var hasOdds = threeMonths.TotalCases > 0;

        return new ScreenerRow
        {
            Symbol = asset.Symbol,
            Name = asset.Name,
            MarketType = asset.MarketType,
            CurrentPrice = odds.CurrentPrice,
            Odds3M = hasOdds ? threeMonths.PositiveOdds : null,
            BaseRate3M = hasOdds ? threeMonths.BaseRate : null,
            Edge3M = hasOdds ? threeMonths.Edge : null,
            Stance = odds.TradeRead?.Stance,
            Strength = odds.TradeRead?.Strength,
            ComputedAt = computedAt,
        };
    }
}
