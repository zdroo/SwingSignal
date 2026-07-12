using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Application.Common;

public static class SymbolNormalizer
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"]       = "BTCUSDT",
        ["BTC/USD"]   = "BTCUSDT",
        ["BTCUSD"]    = "BTCUSDT",
        ["ETH"]       = "ETHUSDT",
        ["ETH/USD"]   = "ETHUSDT",
        ["ETHUSD"]    = "ETHUSDT",
        ["SOL"]       = "SOLUSDT",
        ["SOL/USD"]   = "SOLUSDT",
        ["BNB"]       = "BNBUSDT",
        ["EUR/USD"]   = "EURUSD=X",
        ["EURUSD"]    = "EURUSD=X",
        ["GBP/USD"]   = "GBPUSD=X",
        ["GBPUSD"]    = "GBPUSD=X",
        ["USD/JPY"]   = "USDJPY=X",
        ["USDJPY"]    = "USDJPY=X",
        ["USD/CHF"]   = "USDCHF=X",
        ["AUD/USD"]   = "AUDUSD=X",
        ["GOLD"]      = "GC=F",
        ["XAU"]       = "GC=F",
        ["XAU/USD"]   = "GC=F",
        ["OIL"]       = "CL=F",
        ["WTI"]       = "CL=F",
        ["CRUDE"]     = "CL=F",
        ["SILVER"]    = "SI=F",
        ["XAG"]       = "SI=F",
        ["S&P500"]    = "SPY",
        ["SP500"]     = "SPY",
        ["S&P 500"]   = "SPY",
        ["NASDAQ"]    = "QQQ",
        ["NDX"]       = "QQQ",
    };

    public static string Normalize(string symbol)
    {
        var trimmed = symbol.Trim();
        if (Aliases.TryGetValue(trimmed, out var canonical))
            return canonical;
        return trimmed.ToUpperInvariant();
    }

    public static MarketType DetectMarketType(string symbol)
    {
        if (symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase) ||
            symbol.EndsWith("BUSD", StringComparison.OrdinalIgnoreCase))
            return MarketType.Crypto;

        if (symbol.EndsWith("=X", StringComparison.OrdinalIgnoreCase))
            return MarketType.Forex;

        if (symbol.EndsWith("=F", StringComparison.OrdinalIgnoreCase))
            return MarketType.Commodity;

        return MarketType.Stock;
    }
}
