using Microsoft.EntityFrameworkCore;
using SwingSignal.Application.DTOs;
using SwingSignal.Application.Interfaces;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.Services;

public class HistoricalOddsService : IHistoricalOddsService
{
    private readonly SwingSignalDbContext _db;
    private readonly IMacroRegimeService _regime;

    public HistoricalOddsService(SwingSignalDbContext db, IMacroRegimeService regime)
    {
        _db = db;
        _regime = regime;
    }

    public async Task<AssetOddsDto> GetOddsAsync(string symbol, int topK = 10, CancellationToken ct = default)
    {
        var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Symbol == symbol.ToUpper(), ct)
            ?? throw new KeyNotFoundException($"Asset {symbol.ToUpper()} not found");

        var matches = await _regime.FindSimilarPeriodsAsync(topK, ct);

        if (matches.Count == 0)
            return EmptyOdds(symbol);

        var candles = await _db.Candles
            .Where(c => c.AssetId == asset.Id && c.Interval == CandleInterval.OneDay)
            .OrderBy(c => c.OpenTime)
            .ToListAsync(ct);

        if (candles.Count == 0)
            return EmptyOdds(symbol);

        var matchDates = matches.Select(m => m.Date).ToList();

        var returns1M = new List<decimal>();
        var returns3M = new List<decimal>();
        var returns6M = new List<decimal>();

        foreach (var matchDate in matchDates)
        {
            var entryCandle = FindNearestCandle(candles, matchDate);
            if (entryCandle is null) continue;

            var candle1M = FindNearestCandle(candles, matchDate.AddDays(30));
            var candle3M = FindNearestCandle(candles, matchDate.AddDays(90));
            var candle6M = FindNearestCandle(candles, matchDate.AddDays(180));

            if (candle1M is not null && candle1M.OpenTime > entryCandle.OpenTime)
                returns1M.Add(PercentReturn(entryCandle.Close, candle1M.Close));

            if (candle3M is not null && candle3M.OpenTime > entryCandle.OpenTime)
                returns3M.Add(PercentReturn(entryCandle.Close, candle3M.Close));

            if (candle6M is not null && candle6M.OpenTime > entryCandle.OpenTime)
                returns6M.Add(PercentReturn(entryCandle.Close, candle6M.Close));
        }

        return new AssetOddsDto(
            Symbol: asset.Symbol,
            MatchesUsed: matches.Count,
            OneMonth: ComputeOdds(returns1M),
            ThreeMonths: ComputeOdds(returns3M),
            SixMonths: ComputeOdds(returns6M),
            Disclaimer: "Historical data only. Past macro environments do not guarantee future performance. Not financial advice."
        );
    }

    private static OddsForPeriodDto ComputeOdds(List<decimal> returns)
    {
        if (returns.Count == 0)
            return new OddsForPeriodDto(0, 0, 0, 0, 0, 0, 0);

        var positive = returns.Count(r => r > 0);
        var sorted = returns.OrderBy(r => r).ToList();
        var median = sorted[sorted.Count / 2];

        return new OddsForPeriodDto(
            TotalCases: returns.Count,
            PositiveCases: positive,
            PositiveOdds: Math.Round((double)positive / returns.Count * 100, 1),
            AverageReturn: Math.Round(returns.Average(), 2),
            MedianReturn: Math.Round(median, 2),
            BestCase: Math.Round(returns.Max(), 2),
            WorstCase: Math.Round(returns.Min(), 2)
        );
    }

    private static Domain.Entities.Candle? FindNearestCandle(
        List<Domain.Entities.Candle> candles, DateTime target)
    {
        // Find closest candle within a 7-day window
        return candles
            .Where(c => Math.Abs((c.OpenTime - target).TotalDays) <= 7)
            .MinBy(c => Math.Abs((c.OpenTime - target).TotalDays));
    }

    private static decimal PercentReturn(decimal entry, decimal exit) =>
        Math.Round((exit - entry) / entry * 100, 2);

    private static AssetOddsDto EmptyOdds(string symbol) =>
        new(symbol, 0,
            new OddsForPeriodDto(0, 0, 0, 0, 0, 0, 0),
            new OddsForPeriodDto(0, 0, 0, 0, 0, 0, 0),
            new OddsForPeriodDto(0, 0, 0, 0, 0, 0, 0),
            "Insufficient data to compute odds.");
}
