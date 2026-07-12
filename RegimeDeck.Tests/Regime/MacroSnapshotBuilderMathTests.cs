using RegimeDeck.Application.Regime;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Tests.Regime;

public class MacroSnapshotBuilderMathTests
{
    // Eight dims across seven families — the minimum comfortable comparison set
    private static readonly MacroIndicatorType[] Dims =
    [
        MacroIndicatorType.FedFundsRate,      // family 0
        MacroIndicatorType.TreasuryYield10Y,  // family 1
        MacroIndicatorType.TreasuryYield2Y,   // family 1
        MacroIndicatorType.CPI,               // family 2
        MacroIndicatorType.UnemploymentRate,  // family 3
        MacroIndicatorType.GDP,               // family 4
        MacroIndicatorType.VIX,               // family 5
        MacroIndicatorType.GoldPrice,         // family 6
    ];

    [Fact]
    public void ToYoYSeries_ComputesExactPercentChangeVsSameMonthLastYear()
    {
        var monthly = new Dictionary<DateTime, decimal>
        {
            [new DateTime(2020, 1, 1)] = 100m,
            [new DateTime(2021, 1, 1)] = 110m,
            [new DateTime(2022, 1, 1)] = 99m,
        };

        var yoy = MacroSnapshotBuilder.ToYoYSeries(monthly);

        Assert.Equal(2, yoy.Count);
        Assert.Equal(10m, yoy[new DateTime(2021, 1, 1)]);
        Assert.Equal(-10m, yoy[new DateTime(2022, 1, 1)]);
    }

    [Fact]
    public void ToYoYSeries_NegativeBaseline_UsesAbsoluteValue()
    {
        var monthly = new Dictionary<DateTime, decimal>
        {
            [new DateTime(2020, 1, 1)] = -50m,
            [new DateTime(2021, 1, 1)] = -25m,
        };

        var yoy = MacroSnapshotBuilder.ToYoYSeries(monthly);

        Assert.Equal(50m, yoy[new DateTime(2021, 1, 1)]); // improved by 50% of |baseline|
    }

    [Fact]
    public void ToYoYSeries_ZeroBaseline_SkipsTheMonth()
    {
        var monthly = new Dictionary<DateTime, decimal>
        {
            [new DateTime(2020, 1, 1)] = 0m,
            [new DateTime(2021, 1, 1)] = 5m,
        };

        Assert.Empty(MacroSnapshotBuilder.ToYoYSeries(monthly));
    }

    [Fact]
    public void ComputeNormalizationStats_ExactMeanAndStdDev()
    {
        // FedFundsRate values 1..12 => mean 6.5, population stddev sqrt(143/12)
        var snapshots = Enumerable.Range(1, 12)
            .Select(i => new MonthlySnapshot(
                new DateTime(2020, i, 1),
                new Dictionary<MacroIndicatorType, decimal> { [MacroIndicatorType.FedFundsRate] = i }))
            .ToList();

        var stats = MacroSnapshotBuilder.ComputeNormalizationStats(snapshots);

        var (mean, stdDev) = stats[MacroIndicatorType.FedFundsRate];
        Assert.Equal(6.5m, mean);
        Assert.Equal(3.452053m, Math.Round(stdDev, 6)); // sqrt(11.91666...) = 3.4520525...
    }

    [Fact]
    public void ComputeNormalizationStats_FewerThan12Values_ExcludesIndicator()
    {
        var snapshots = Enumerable.Range(1, 11)
            .Select(i => new MonthlySnapshot(
                new DateTime(2020, i, 1),
                new Dictionary<MacroIndicatorType, decimal> { [MacroIndicatorType.FedFundsRate] = i }))
            .ToList();

        Assert.Empty(MacroSnapshotBuilder.ComputeNormalizationStats(snapshots));
    }

    [Fact]
    public void BuildZScoreVector_ExactZScores()
    {
        var stats = new Dictionary<MacroIndicatorType, (decimal Mean, decimal StdDev)>
        {
            [MacroIndicatorType.FedFundsRate] = (2m, 2m),
        };
        var values = new Dictionary<MacroIndicatorType, decimal>
        {
            [MacroIndicatorType.FedFundsRate] = 6m,
            [MacroIndicatorType.VIX] = 20m, // no stats -> excluded
        };

        var vector = MacroSnapshotBuilder.BuildZScoreVector(values, stats);

        Assert.Single(vector);
        Assert.Equal(2.0, vector[MacroIndicatorType.FedFundsRate]);
    }

    private static Dictionary<MacroIndicatorType, double> Vector(Func<MacroIndicatorType, double> value) =>
        Dims.ToDictionary(d => d, value);

    [Fact]
    public void SharedDimensionDistance_FlatMode_IsRmsOverAllSharedDims()
    {
        var a = Vector(_ => 0);
        var b = Vector(d => d == MacroIndicatorType.TreasuryYield10Y ? 3.0 : 1.0);

        // squared diffs: 9 for 10Y, 1 for the other seven => sqrt(16/8) = sqrt(2)
        var distance = MacroSnapshotBuilder.SharedDimensionDistance(a, b, familyWeighting: false);

        Assert.NotNull(distance);
        Assert.Equal(Math.Sqrt(2), distance.Value, precision: 12);
    }

    [Fact]
    public void SharedDimensionDistance_FamilyMode_AveragesWithinFamiliesFirst()
    {
        var a = Vector(_ => 0);
        var b = Vector(d => d == MacroIndicatorType.TreasuryYield10Y ? 3.0 : 1.0);

        // rates family mean: (9+1)/2 = 5; other six families: 1 each => sqrt(11/7)
        var distance = MacroSnapshotBuilder.SharedDimensionDistance(a, b, familyWeighting: true);

        Assert.NotNull(distance);
        Assert.Equal(Math.Sqrt(11.0 / 7.0), distance.Value, precision: 12);
    }

    [Fact]
    public void SharedDimensionDistance_TooFewSharedDims_ReturnsNull()
    {
        var a = Vector(_ => 0);
        var b = Vector(_ => 1.0);
        b.Remove(MacroIndicatorType.GoldPrice); // 7 shared < MinSharedDimensions (8)

        Assert.Null(MacroSnapshotBuilder.SharedDimensionDistance(a, b));
    }

    [Fact]
    public void SharedDimensionDistance_TooFewFamilies_ReturnsNull()
    {
        // 8 dims but only 2 families (policy + rates)
        MacroIndicatorType[] narrow =
        [
            MacroIndicatorType.FedFundsRate, MacroIndicatorType.RealYield10Y, MacroIndicatorType.FedBalanceSheet,
            MacroIndicatorType.TreasuryYield10Y, MacroIndicatorType.TreasuryYield2Y, MacroIndicatorType.TreasuryYield3M,
            MacroIndicatorType.YieldCurveSpread, MacroIndicatorType.YieldSpread10Y3M,
        ];
        var a = narrow.ToDictionary(d => d, _ => 0.0);
        var b = narrow.ToDictionary(d => d, _ => 1.0);

        Assert.Null(MacroSnapshotBuilder.SharedDimensionDistance(a, b));
    }

    [Fact]
    public void KernelWeights_GaussianWithMedianBandwidth()
    {
        // similarities 50 and 100/3 => distances 1 and 2; median (idx len/2) = 2
        var weights = MacroSnapshotBuilder.KernelWeights([50.0, 100.0 / 3.0]);

        Assert.Equal(2, weights.Length);
        Assert.Equal(Math.Exp(-0.25), weights[0], precision: 12); // (1/2)^2
        Assert.Equal(Math.Exp(-1.0), weights[1], precision: 12);  // (2/2)^2
    }

    // ── FindMatches on a synthetic 30-month history ──────────────────────

    private static List<MonthlySnapshot> LinearSnapshots(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new MonthlySnapshot(
                new DateTime(2020, 1, 1).AddMonths(i),
                Dims.ToDictionary(d => d, _ => (decimal)i)))
            .ToList();

    [Fact]
    public void FindMatches_Declustered_PicksRepresentativesSixMonthsApart()
    {
        var snapshots = LinearSnapshots(30); // 2020-01 .. 2022-06; current = 2022-06

        var matches = MacroSnapshotBuilder.FindMatches(snapshots, asOfIndex: 29, topK: 4);

        // Eligible candidates: everything <= 2021-12 (24 of them). Distance grows
        // with month gap, so best is 2021-12, then each pick skips 5 neighbours.
        Assert.Equal(4, matches.Count);
        Assert.Equal(new DateTime(2021, 12, 1), matches[0].Snapshot.Date);
        Assert.Equal(new DateTime(2021, 6, 1), matches[1].Snapshot.Date);
        Assert.Equal(new DateTime(2020, 12, 1), matches[2].Snapshot.Date);
        Assert.Equal(new DateTime(2020, 6, 1), matches[3].Snapshot.Date);

        // Rank reflects standing in the FULL candidate list, not the declustered one
        Assert.Equal(1, matches[0].Rank);
        Assert.Equal(7, matches[1].Rank);
        Assert.Equal(13, matches[2].Rank);
        Assert.Equal(19, matches[3].Rank);
        Assert.All(matches, m => Assert.Equal(24, m.CandidateCount));

        // Similarity strictly decreasing
        Assert.True(matches[0].Similarity > matches[1].Similarity);
        Assert.True(matches[1].Similarity > matches[2].Similarity);
        Assert.True(matches[2].Similarity > matches[3].Similarity);
    }

    [Fact]
    public void FindMatches_WithoutDecluster_PicksAdjacentMonths()
    {
        var snapshots = LinearSnapshots(30);
        var options = MatchingOptions.Production with { Decluster = false };

        var matches = MacroSnapshotBuilder.FindMatches(snapshots, asOfIndex: 29, topK: 4, options);

        Assert.Equal(new DateTime(2021, 12, 1), matches[0].Snapshot.Date);
        Assert.Equal(new DateTime(2021, 11, 1), matches[1].Snapshot.Date);
        Assert.Equal(new DateTime(2021, 10, 1), matches[2].Snapshot.Date);
        Assert.Equal(new DateTime(2021, 9, 1), matches[3].Snapshot.Date);
        Assert.Equal(new[] { 1, 2, 3, 4 }, matches.Select(m => m.Rank).ToArray());
    }

    [Fact]
    public void FindMatches_ExcludesTheLastSixMonths()
    {
        var snapshots = LinearSnapshots(30);

        var matches = MacroSnapshotBuilder.FindMatches(snapshots, asOfIndex: 29, topK: 24);

        // 2022-01 .. 2022-05 must never appear (within 6 months of 2022-06)
        var cutoff = new DateTime(2021, 12, 1);
        Assert.All(matches, m => Assert.True(m.Snapshot.Date <= cutoff));
    }

    [Fact]
    public void FindMatches_MinCandidateDate_ExcludesEarlierMonths()
    {
        var snapshots = LinearSnapshots(30); // 2020-01 .. 2022-06
        var floor = new DateTime(2021, 1, 1);

        var matches = MacroSnapshotBuilder.FindMatches(
            snapshots, asOfIndex: 29, topK: 24, minCandidateDate: floor);

        // Eligible: 2021-01 .. 2021-12 only (12 candidates); 2020 never appears
        Assert.All(matches, m => Assert.True(m.Snapshot.Date >= floor));
        Assert.All(matches, m => Assert.Equal(12, m.CandidateCount));
    }

    // Six-dim filter across six families: minShared becomes 6/2 = 3, minFamilies 3
    private static readonly MacroIndicatorType[] TestFilter =
    [
        MacroIndicatorType.FedFundsRate,      // family 0
        MacroIndicatorType.TreasuryYield10Y,  // family 1
        MacroIndicatorType.CPI,               // family 2
        MacroIndicatorType.UnemploymentRate,  // family 3
        MacroIndicatorType.GDP,               // family 4
        MacroIndicatorType.VIX,               // family 5
    ];

    [Fact]
    public void FindMatches_DimensionFilter_IgnoresExcludedDimensions()
    {
        // All snapshots linear in every dim, EXCEPT 2020-06: it equals the
        // current month exactly on the filtered dims but is wildly off on the
        // excluded GoldPrice dim.
        var snapshots = LinearSnapshots(30);
        var planted = Dims.ToDictionary(d => d, _ => 29m);
        planted[MacroIndicatorType.GoldPrice] = 1000m;
        snapshots[5] = new MonthlySnapshot(new DateTime(2020, 6, 1), planted);

        var filtered = MacroSnapshotBuilder.FindMatches(
            snapshots, asOfIndex: 29, topK: 4,
            MatchingOptions.Production with { DimensionFilter = TestFilter });

        // Gold is invisible under the filter: the planted month is a perfect match
        Assert.Equal(new DateTime(2020, 6, 1), filtered[0].Snapshot.Date);
        Assert.Equal(100.0, filtered[0].Similarity, precision: 6); // distance 0

        var unfiltered = MacroSnapshotBuilder.FindMatches(snapshots, asOfIndex: 29, topK: 4);

        // With all dims, the huge gold gap disqualifies it from the top slot
        Assert.Equal(new DateTime(2021, 12, 1), unfiltered[0].Snapshot.Date);
    }

    [Fact]
    public void MatchingOptions_ForMarket_SelectsProfileByMarketAndHorizon()
    {
        // Crypto splits at the a-priori 45-day boundary
        Assert.Same(MatchingOptions.CryptoShortHorizon, MatchingOptions.ForMarket(MarketType.Crypto, 30));
        Assert.Same(MatchingOptions.CryptoShortHorizon, MatchingOptions.ForMarket(MarketType.Crypto, 45));
        Assert.Same(MatchingOptions.CryptoProduction, MatchingOptions.ForMarket(MarketType.Crypto, 46));
        Assert.Same(MatchingOptions.CryptoProduction, MatchingOptions.ForMarket(MarketType.Crypto, 180));

        // Non-crypto: one profile at every horizon
        Assert.Same(MatchingOptions.Production, MatchingOptions.ForMarket(MarketType.Stock, 30));
        Assert.Same(MatchingOptions.Production, MatchingOptions.ForMarket(MarketType.Index, 180));

        Assert.Equal("crypto", MatchingOptions.CryptoProduction.Label);
        Assert.Equal("crypto-short", MatchingOptions.CryptoShortHorizon.Label);
        Assert.Equal(MacroSnapshotBuilder.CryptoDimensions, MatchingOptions.CryptoProduction.DimensionFilter);
    }

    [Fact]
    public void CryptoNativeDimensions_NeverLeakIntoStockMatching()
    {
        // Production (stocks/ETFs/forex) filters to the macro-only fingerprint
        Assert.Equal(MacroSnapshotBuilder.MacroDimensions, MatchingOptions.Production.DimensionFilter);
        Assert.All(MacroSnapshotBuilder.CryptoNativeIndicators, d =>
            Assert.DoesNotContain(d, MacroSnapshotBuilder.MacroDimensions));

        // Natives belong to the short-horizon profile only; the long-horizon
        // crypto profile stays macro-only (walk-forward validated split)
        Assert.All(MacroSnapshotBuilder.CryptoNativeIndicators, d =>
        {
            Assert.DoesNotContain(d, MacroSnapshotBuilder.CryptoDimensions);
            Assert.Contains(d, MacroSnapshotBuilder.CryptoDimensionsWithNatives);
        });
        Assert.Equal(MacroSnapshotBuilder.CryptoDimensionsWithNatives,
            MatchingOptions.CryptoShortHorizon.DimensionFilter);

        Assert.True(MatchingOptions.CryptoProduction.FloorAnalogsToAssetHistory);
        Assert.True(MatchingOptions.CryptoShortHorizon.FloorAnalogsToAssetHistory);
        Assert.False(MatchingOptions.Production.FloorAnalogsToAssetHistory);
    }
}
