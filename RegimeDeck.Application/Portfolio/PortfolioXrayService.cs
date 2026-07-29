using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Abstractions.Persistence;
using RegimeDeck.Application.Common;
using RegimeDeck.Application.Liquidity;
using RegimeDeck.Application.Regime;
using RegimeDeck.Contracts.Portfolio;
using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Portfolio;

public class PortfolioXrayService : IPortfolioXrayService
{
    private const int MaxHoldings = 25;
    private const int MinCandles = 30;
    private const int HorizonDays = 90;

    private const string Note =
        "A macro exposure lens, not advice. Outcomes are how a book with these weights fared over the " +
        "3 months after historical months whose macro resembled today's — a small, overlapping sample, " +
        "so read it as risk and lean, not a forecast. Nothing here is a recommendation to buy or sell.";

    private readonly IAssetIngestionService _ingestion;
    private readonly ICandleRepository _candles;
    private readonly IMacroRegimeService _regime;
    private readonly ILiquidityService _liquidity;

    public PortfolioXrayService(
        IAssetIngestionService ingestion,
        ICandleRepository candles,
        IMacroRegimeService regime,
        ILiquidityService liquidity)
    {
        _ingestion = ingestion;
        _candles = candles;
        _regime = regime;
        _liquidity = liquidity;
    }

    private sealed record Loaded(Asset Asset, List<Candle> Candles, decimal Value, string Class, bool RiskOn);

    public async Task<PortfolioXrayDto> GetXrayAsync(PortfolioXrayRequest request, CancellationToken ct = default)
    {
        var inputs = request?.Holdings ?? [];
        if (inputs.Count == 0)
            throw new ValidationException("Add at least one holding.");
        if (inputs.Count > MaxHoldings)
            throw new ValidationException($"Up to {MaxHoldings} holdings at a time.");
        if (inputs.Any(h => h.Value <= 0m))
            throw new ValidationException("Each holding needs a positive dollar value.");

        var loaded = await LoadHoldingsAsync(inputs, ct);
        var weights = PortfolioMath.NormalizeWeights([.. loaded.Select(l => l.Value)]);

        // One shared macro analog set across the whole book (macro-only profile),
        // so every holding's returns line up on the same months.
        var matches = await _regime.FindSimilarPeriodsAsync(
            MatchingOptions.AnalogCount, MatchingOptions.Production, null, ct);
        var kernel = matches.Count > 0
            ? MacroSnapshotBuilder.KernelWeights([.. matches.Select(m => m.SimilarityScore)])
            : [];

        var perHolding = loaded.Select(l => ForwardReturns(l.Candles, matches)).ToList();
        var outcome = BuildOutcome(loaded.Count, matches.Count, perHolding, weights, kernel);

        var liquiditySeries = await LoadLiquiditySeriesAsync(ct);

        var holdings = new List<HoldingXrayDto>();
        for (var i = 0; i < loaded.Count; i++)
        {
            var l = loaded[i];
            var got = perHolding[i].Where(r => r is not null).Select(r => r!.Value).ToList();
            var vol = PortfolioMath.AnnualizedVolatilityPct([.. l.Candles.TakeLast(252).Select(c => c.Close)]);
            holdings.Add(new HoldingXrayDto(
                l.Asset.Symbol, l.Asset.Name, l.Class, PortfolioMath.Posture(l.RiskOn),
                Math.Round(weights[i] * 100, 1),
                got.Count > 0 ? Median(got) : 0m,
                got.Count > 0 ? got.Min() : 0m,
                vol is null ? null : Math.Round(vol.Value, 1),
                LiquidityBeta(l.Candles, liquiditySeries)));
        }

        var concentration = BuildConcentration(loaded, weights);
        var exposure = BuildExposure(loaded, weights, holdings);
        var regimeSummary = await RegimeSummaryAsync(ct);

        return new PortfolioXrayDto(
            regimeSummary, holdings, concentration, exposure, outcome,
            BuildReads(concentration, exposure, outcome, holdings, weights), Note);
    }

    private async Task<List<Loaded>> LoadHoldingsAsync(List<PortfolioHoldingInput> inputs, CancellationToken ct)
    {
        var loaded = new List<Loaded>(inputs.Count);
        foreach (var h in inputs)
        {
            var normalized = SymbolNormalizer.Normalize(h.Symbol);
            var asset = await _ingestion.EnsureIngestedAsync(normalized, ct)
                ?? throw new ValidationException($"Couldn't analyze \"{h.Symbol}\" — unknown or unsupported ticker.");

            var candles = await _candles.GetDailyHistoryAsync(asset.Id, ct);
            if (candles.Count < MinCandles)
                throw new ValidationException($"Not enough price history for {asset.Symbol} yet — try again shortly.");

            var (cls, riskOn) = PortfolioMath.Classify(asset.Symbol, asset.MarketType);
            loaded.Add(new Loaded(asset, candles, h.Value, cls, riskOn));
        }
        return loaded;
    }

    // 3-month forward return at each analog month; null where the asset lacks data.
    private static decimal?[] ForwardReturns(List<Candle> candles, List<HistoricalMatchDto> matches)
    {
        var exitWindow = CandleMath.ExitWindow(HorizonDays);
        var returns = new decimal?[matches.Count];
        for (var a = 0; a < matches.Count; a++)
        {
            var entry = CandleMath.FindNearest(candles, matches[a].Date, 7);
            if (entry is null) continue;
            var exit = CandleMath.FindNearest(candles, matches[a].Date.AddDays(HorizonDays), exitWindow);
            if (exit is null || exit.OpenTime <= entry.OpenTime) continue;
            returns[a] = CandleMath.PercentReturn(entry.Close, exit.Close);
        }
        return returns;
    }

    // Portfolio return per analog month where EVERY holding has data — so each
    // month reflects the full book and diversification nets out.
    private static PortfolioOutcomeDto BuildOutcome(
        int holdingCount, int analogCount, List<decimal?[]> perHolding, double[] weights, double[] kernel)
    {
        var samples = new List<(decimal Value, double Weight)>();
        for (var a = 0; a < analogCount; a++)
        {
            var complete = true;
            decimal portfolioReturn = 0m;
            for (var i = 0; i < holdingCount; i++)
            {
                if (perHolding[i][a] is not decimal r) { complete = false; break; }
                portfolioReturn += (decimal)weights[i] * r;
            }
            if (complete)
                samples.Add((portfolioReturn, kernel.Length > a ? kernel[a] : 1.0));
        }

        if (samples.Count == 0)
            return new PortfolioOutcomeDto(0, PortfolioMath.Confidence(0), 0, 0, 0, 0);

        var sorted = samples.OrderBy(s => s.Value).ToList();
        var total = samples.Sum(s => s.Weight);
        return new PortfolioOutcomeDto(
            samples.Count,
            PortfolioMath.Confidence(samples.Count),
            Math.Round(PortfolioMath.WeightedPositiveOdds(samples), 1),
            Math.Round(PortfolioMath.WeightedPercentile(sorted, total, 0.50), 2),
            Math.Round(sorted[0].Value, 2),
            Math.Round(sorted[^1].Value, 2));
    }

    private static ConcentrationDto BuildConcentration(List<Loaded> loaded, double[] weights)
    {
        var hhi = PortfolioMath.Hhi(weights);
        var byClass = loaded
            .Select((l, i) => (l.Class, W: weights[i]))
            .GroupBy(x => x.Class)
            .Select(g => new AssetClassWeightDto(g.Key, Math.Round(g.Sum(x => x.W) * 100, 1)))
            .OrderByDescending(c => c.WeightPct)
            .ToList();

        var descending = weights.OrderByDescending(w => w).ToList();
        return new ConcentrationDto(
            Math.Round(descending[0] * 100, 1),
            Math.Round(descending.Take(3).Sum() * 100, 1),
            hhi,
            PortfolioMath.ConcentrationLabel(hhi),
            byClass);
    }

    private static ExposureDto BuildExposure(List<Loaded> loaded, double[] weights, List<HoldingXrayDto> holdings)
    {
        double riskOn = 0;
        for (var i = 0; i < loaded.Count; i++)
            if (loaded[i].RiskOn) riskOn += weights[i];
        var riskOnPct = Math.Round(riskOn * 100, 1);

        var withBeta = holdings
            .Select((h, i) => (h.LiquidityBeta, W: weights[i]))
            .Where(x => x.LiquidityBeta.HasValue)
            .ToList();
        var betaWeight = withBeta.Sum(x => x.W);
        var beta = betaWeight > 0
            ? (int)Math.Round(withBeta.Sum(x => x.LiquidityBeta!.Value * x.W) / betaWeight)
            : 0;

        return new ExposureDto(riskOnPct, Math.Round(100 - riskOnPct, 1), beta, PortfolioMath.LiquidityLabel(beta));
    }

    private static List<string> BuildReads(
        ConcentrationDto c, ExposureDto e, PortfolioOutcomeDto o, List<HoldingXrayDto> holdings, double[] weights)
    {
        var reads = new List<string>();
        var top = holdings[Array.IndexOf(weights, weights.Max())];

        reads.Add(c.Label == "Concentrated"
            ? $"Concentrated book — {top.WeightPct:0.#}% sits in {top.Symbol}. One position drives most of the risk."
            : c.Label == "Moderate"
            ? $"Moderately concentrated — the top holding is {c.TopWeightPct:0.#}% of the book."
            : $"Reasonably diversified — no single holding dominates (top {c.TopWeightPct:0.#}%).");

        reads.Add($"{e.RiskOnPct:0.#}% risk-on, {e.DefensivePct:0.#}% defensive — " +
            (e.RiskOnPct >= 70 ? "this book leans hard into risk appetite."
             : e.RiskOnPct <= 30 ? "this book is positioned defensively."
             : "a balanced risk posture."));

        reads.Add(e.LiquidityLabel == "Liquidity-driven"
            ? "Liquidity-driven — your returns have tracked global-liquidity swings closely, so watch the tide."
            : e.LiquidityLabel == "Liquidity-insulated"
            ? "Largely insulated from global liquidity — the tide matters less here."
            : "Partly tied to global liquidity.");

        if (o.Analogs > 0)
            reads.Add($"In regimes like today, a book like yours had a median 3-month outcome of " +
                $"{o.MedianReturn:+0.0;-0.0}% (worst analog {o.WorstReturn:+0.0;-0.0}%), across {o.Analogs} analog months " +
                $"— {o.Confidence.ToLowerInvariant()} confidence.");

        return reads;
    }

    private async Task<List<(DateTime Date, decimal Value)>> LoadLiquiditySeriesAsync(CancellationToken ct)
    {
        var dashboard = await _liquidity.GetDashboardAsync(ct);
        return [.. dashboard.Series.Select(p => (p.Date, p.GlobalLiquidity))];
    }

    private static int? LiquidityBeta(List<Candle> candles, List<(DateTime Date, decimal Value)> liquidity)
    {
        if (candles.Count == 0 || liquidity.Count < 76) return null;

        var assetSeries = candles.Select(c => (c.OpenTime, c.Close)).ToList();
        var start = candles[0].OpenTime;

        var liq = new List<decimal>();
        var asset = new List<decimal>();
        foreach (var (date, value) in liquidity)
        {
            if (date < start) continue;
            var close = LiquidityMath.AsOf(assetSeries, date);
            if (close is null) continue;
            liq.Add(value);
            asset.Add(close.Value);
        }

        return PortfolioMath.LiquidityCorrelation(asset, liq);
    }

    private async Task<string> RegimeSummaryAsync(CancellationToken ct)
    {
        var regime = await _regime.GetCurrentRegimeAsync(ct);
        var lead = regime.Summary.Count > 0 ? regime.Summary[0] : null;
        return lead ?? $"{regime.Health.Label} market health.";
    }

    private static decimal Median(List<decimal> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }
}
