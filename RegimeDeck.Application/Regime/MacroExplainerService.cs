using RegimeDeck.Contracts.Regime;
using RegimeDeck.Domain.Entities;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Regime;

public class MacroExplainerService : IAssetExplainerService
{
    private readonly IMacroRegimeService _regime;

    public MacroExplainerService(IMacroRegimeService regime) => _regime = regime;

    public async Task<List<string>> GenerateAsync(
        string symbol,
        MarketType marketType,
        List<HistoricalMatchDto> matches,
        List<Candle>? candles = null,
        CancellationToken ct = default)
    {
        var regime = await _regime.GetCurrentRegimeAsync(ct);
        var bullets = new List<string>();

        AddFedBullet(bullets, regime);
        AddLiquidityBullet(bullets, regime);
        AddYieldCurveBullet(bullets, regime);
        AddInflationBullet(bullets, regime);
        AddUnemploymentBullet(bullets, regime);
        AddVixBullet(bullets, regime);
        AddCreditStressBullet(bullets, regime);
        AddGoldBullet(bullets, regime);
        AddAssetTypeBullet(bullets, regime, marketType);
        AddCryptoCycleBullets(bullets, marketType, candles);
        AddMatchesBullet(bullets, matches);

        return bullets;
    }

    // Crypto-native cycle context — needs no external data sources
    private static void AddCryptoCycleBullets(List<string> bullets, MarketType marketType, List<Candle>? candles)
    {
        if (marketType != MarketType.Crypto) return;

        var halving = CryptoCycle.HalvingBullet(DateTime.UtcNow);
        if (halving is not null) bullets.Add(halving);

        if (candles is not null)
        {
            var mayer = CryptoCycle.MayerBullet(candles);
            if (mayer is not null) bullets.Add(mayer);
        }
    }

    private static void AddFedBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("FedFundsRate", out var fed)) return;

        var bullet = fed.Signal switch
        {
            "Restrictive" =>
                $"Fed is restrictive at {fed.Value:F2}% - the highest cost of money in years. " +
                "High rates compress valuations and reduce liquidity. Historically this creates short-term headwinds for risk assets, " +
                "but markets often rally 6-12 months before the first rate cut in anticipation of easing.",

            "Neutral" =>
                $"Fed funds rate is neutral at {fed.Value:F2}% - neither stimulating nor restraining the economy. " +
                "In past neutral periods, markets traded primarily on earnings momentum rather than macro direction.",

            "Accommodative" =>
                $"Fed is accommodative at {fed.Value:F2}% - cheap money is actively flowing into the economy. " +
                "Historically the strongest tailwind for risk assets: equities, crypto, and commodities all tend to outperform in easy-money environments.",

            _ => $"Fed funds rate at {fed.Value:F2}%."
        };

        bullets.Add(bullet);
    }

    private static void AddYieldCurveBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("YieldCurveSpread", out var yc)) return;

        var bullet = yc.Signal switch
        {
            "Inverted" =>
                $"Yield curve inverted at {yc.Value:F2}% (10Y minus 2Y) - the short end yields more than the long end, " +
                "a condition that has preceded every US recession since 1970. Historically risk assets fall during the inversion " +
                "but often recover strongly 12-18 months after the curve re-steepens.",

            "Flat" =>
                $"Yield curve flat at {yc.Value:F2}% - the market is uncertain about growth direction. " +
                "Flat curves historically precede either a soft landing (bullish) or further inversion (bearish).",

            "Normal" =>
                $"Yield curve healthy at {yc.Value:F2}% - long-term rates exceed short-term rates, signalling growth confidence. " +
                "Historically a favorable backdrop for equities and risk assets.",

            _ => $"Yield curve spread at {yc.Value:F2}%."
        };

        bullets.Add(bullet);
    }

    private static void AddInflationBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("CPI", out var cpi)) return;

        var bullet = cpi.Signal switch
        {
            "Elevated" =>
                $"Inflation elevated at {cpi.Value:F1}% YoY - persistently high inflation forces the Fed to keep rates high, " +
                "which historically caps equity and crypto upside. Real returns erode for bonds. " +
                "Hard assets like gold and commodities tend to hold value better.",

            "Low" =>
                $"Inflation low at {cpi.Value:F1}% YoY - gives the Fed room to cut rates, " +
                "historically bullish for both equities and crypto. Bonds also benefit from falling yields.",

            _ =>
                $"Inflation at {cpi.Value:F1}% YoY - near the Fed's target range. " +
                "No inflation-driven macro pressure in either direction."
        };

        bullets.Add(bullet);
    }

    private static void AddLiquidityBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("FedBalanceSheet", out var bs)) return;

        var bullet = bs.Signal switch
        {
            "QE (Expanding)" =>
                $"Fed balance sheet expanding at {bs.Value:F1}% YoY - quantitative easing is injecting liquidity into markets. " +
                "Historically the single strongest tailwind for risk assets, and crypto in particular: " +
                "BTC's biggest bull runs (2020-21) coincided with aggressive balance sheet expansion.",

            "QT (Contracting)" =>
                $"Fed balance sheet contracting at {bs.Value:F1}% YoY - quantitative tightening is draining liquidity from markets. " +
                "Historically a persistent headwind for risk assets; both the 2018 and 2022 drawdowns occurred during QT.",

            _ =>
                $"Fed balance sheet roughly stable ({bs.Value:F1}% YoY) - liquidity is neither being injected nor drained. " +
                "Markets trade on fundamentals rather than liquidity flows in this state."
        };

        bullets.Add(bullet);
    }

    private static void AddVixBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("VIX", out var vix)) return;

        var bullet = vix.Signal switch
        {
            "Panic" =>
                $"VIX at {vix.Value:F0} - panic territory. Historically, VIX spikes above 30 have marked capitulation zones: " +
                "forward 6-month returns for equities after panic readings are among the best of any regime, " +
                "though short-term volatility remains extreme.",

            "Elevated" =>
                $"VIX elevated at {vix.Value:F0} - the market is pricing meaningful uncertainty. " +
                "Elevated-but-not-panicked VIX historically resolves in either direction; position sizing matters more than direction here.",

            "Complacent" =>
                $"VIX at {vix.Value:F0} - complacency zone. Low volatility regimes historically persist longer than expected, " +
                "but leave markets vulnerable to sharp corrections when a catalyst arrives.",

            _ =>
                $"VIX at {vix.Value:F0} - normal volatility levels, no strong fear signal in either direction."
        };

        bullets.Add(bullet);
    }

    private static void AddCreditStressBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("HighYieldSpread", out var hy)) return;

        var bullet = hy.Signal switch
        {
            "Stressed" =>
                $"High-yield credit spreads stressed at {hy.Value:F1}% - junk bond investors are demanding a large premium, " +
                "historically one of the most reliable early warnings of recession and equity drawdowns. Credit leads equity.",

            "Elevated" =>
                $"High-yield credit spreads elevated at {hy.Value:F1}% - some stress building in credit markets. " +
                "Worth watching: spreads widening past 6% has historically preceded major equity weakness.",

            _ =>
                $"High-yield credit spreads calm at {hy.Value:F1}% - credit markets see low default risk. " +
                "Historically a supportive backdrop for risk assets; major drawdowns rarely start with calm credit."
        };

        bullets.Add(bullet);
    }

    private static void AddUnemploymentBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("UnemploymentRate", out var unemp)) return;

        var bullet = unemp.Signal switch
        {
            "Elevated" =>
                $"Unemployment elevated at {unemp.Value:F1}% - signals economic weakness and reduced consumer spending. " +
                "Historically risk assets underperform in early recession phases, but the labor market is a lagging indicator: " +
                "markets often bottom well before unemployment peaks.",

            "Healthy" =>
                $"Labor market strong at {unemp.Value:F1}% unemployment - consumer spending is resilient. " +
                "Historically this supports corporate earnings and is a positive backdrop for equities.",

            _ =>
                $"Unemployment at {unemp.Value:F1}% - labor market stable with no strong directional signal."
        };

        bullets.Add(bullet);
    }

    private static void AddGoldBullet(List<string> bullets, MacroRegimeDto regime)
    {
        if (!regime.Indicators.TryGetValue("GoldPrice", out var gold)) return;

        var bullet = gold.Signal switch
        {
            "Risk-off" =>
                $"Gold up {gold.Value:F0}% YoY - strong safe-haven demand signals fear and uncertainty. " +
                "Risk-off gold rallies often coincide with equity weakness, but also tend to precede eventual flight back into risk assets once uncertainty clears.",

            "Risk-on" =>
                $"Gold down {Math.Abs(gold.Value):F0}% YoY - low safe-haven demand. " +
                "Markets historically perform well when gold is not attracting fear-driven flows.",

            _ =>
                $"Gold roughly flat ({gold.Value:F0}% YoY) - no strong risk-on or risk-off signal from precious metals."
        };

        bullets.Add(bullet);
    }

    private static void AddAssetTypeBullet(List<string> bullets, MacroRegimeDto regime, MarketType marketType)
    {
        var fed = regime.Indicators.GetValueOrDefault("FedFundsRate");
        var yc  = regime.Indicators.GetValueOrDefault("YieldCurveSpread");
        var oil = regime.Indicators.GetValueOrDefault("OilWTI");

        var bullet = marketType switch
        {
            MarketType.Crypto => GetCryptoBullet(fed, yc),
            MarketType.Stock or MarketType.Index => GetEquityBullet(fed, yc),
            MarketType.Forex  => GetForexBullet(fed),
            MarketType.Commodity => GetCommodityBullet(oil),
            _ => null
        };

        if (bullet is not null)
            bullets.Add(bullet);
    }

    private static string GetCryptoBullet(MacroIndicatorValueDto? fed, MacroIndicatorValueDto? yc)
    {
        if (fed?.Signal == "Restrictive" && yc?.Signal == "Inverted")
            return "Crypto context: restrictive Fed + inverted yield curve is historically the hardest macro environment for BTC and ETH. " +
                   "In the 3 previous occurrences (2007, 2019, 2022), BTC fell 40-70% but then rallied 200-700% in the 24 months following the Fed's first rate cut. " +
                   "Timing the pivot is the key variable.";

        if (fed?.Signal == "Accommodative")
            return "Crypto context: easy monetary policy is historically the strongest catalyst for BTC and ETH. " +
                   "In every accommodative cycle since 2013, BTC delivered at least 3x returns over 18 months. " +
                   "Liquidity expansion disproportionately benefits high-beta assets like crypto.";

        return "Crypto context: BTC and ETH are highly sensitive to global liquidity. " +
               "Watch the Fed pivot - rate cut cycles have historically been the single strongest catalyst for crypto bull markets. " +
               "On-chain data and halving cycles can amplify or dampen macro-driven moves.";
    }

    private static string GetEquityBullet(MacroIndicatorValueDto? fed, MacroIndicatorValueDto? yc)
    {
        if (fed?.Signal == "Restrictive" && yc?.Signal == "Inverted")
            return "Equity context: restrictive rates + inverted yield curve historically lead to earnings compression and multiple contraction. " +
                   "SPY and QQQ typically bottom 6-9 months before the first rate cut, creating a buying opportunity for patient investors.";

        if (fed?.Signal == "Accommodative")
            return "Equity context: low rates reduce the discount rate on future earnings, expanding valuations. " +
                   "Historically the best equity returns come in the first 12-18 months of an accommodative cycle.";

        return "Equity context: SPY and QQQ historically perform best in early and mid-cycle expansions with stable inflation and healthy earnings growth. " +
               "In late-cycle environments, defensive sectors (healthcare, utilities) tend to outperform growth.";
    }

    private static string GetForexBullet(MacroIndicatorValueDto? fed)
    {
        if (fed?.Signal == "Restrictive")
            return "Forex context: high US rates attract global capital into USD-denominated assets, historically strengthening the dollar. " +
                   "EUR/USD and GBP/USD tend to fall while USD/JPY rises in Fed tightening cycles. " +
                   "Watch for a reversal when the market prices in the first rate cut.";

        if (fed?.Signal == "Accommodative")
            return "Forex context: falling US rates reduce USD yield advantage, historically weakening the dollar. " +
                   "EUR/USD and GBP/USD tend to rally in Fed easing cycles, while USD/JPY falls.";

        return "Forex context: in neutral-rate environments, forex pairs are primarily driven by relative central bank policy divergence. " +
               "ECB and BoJ decisions often matter as much as the Fed for EUR/USD and USD/JPY respectively.";
    }

    private static string GetCommodityBullet(MacroIndicatorValueDto? oil)
    {
        if (oil?.Signal == "Inflationary")
            return $"Commodity context: oil up {oil.Value:F0}% YoY - rising energy costs historically signal supply constraints or geopolitical risk. " +
                   "This supports commodity ETFs and energy stocks but acts as an inflation tax on the broader economy.";

        if (oil?.Signal == "Deflationary")
            return $"Commodity context: oil down {Math.Abs(oil.Value):F0}% YoY - deflationary for energy costs, positive for consumers and transportation sectors, " +
                   "but bearish for commodity producers and energy-linked assets.";

        return "Commodity context: gold and oil historically perform well during high-inflation, risk-off environments as stores of value. " +
               "Real assets tend to outperform when the yield curve is flat or inverted and inflation remains sticky.";
    }

    private static void AddMatchesBullet(List<string> bullets, List<HistoricalMatchDto> matches)
    {
        if (matches.Count == 0) return;

        var dates = matches
            .Take(3)
            .Select(m => m.Date.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        bullets.Add(
            $"Historical basis: the current macro regime most closely resembles {string.Join(", ", dates)}. " +
            "The odds above are calculated from how assets actually performed in the months following each of these periods.");
    }
}
