using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RegimeDeck.Application.Abstractions.Ingestion;
using RegimeDeck.Application.Common;
using RegimeDeck.Contracts.Assets;

namespace RegimeDeck.Infrastructure.ExternalClients;

// Free-text symbol search via Yahoo Finance's (unofficial) search endpoint.
// "apple" -> AAPL, "bitcoin" -> BTC-USD (mapped to our Binance symbol), etc.
public class YahooSymbolSearchClient : ISymbolSearchService
{
    private static readonly HashSet<string> SupportedTypes =
        new(StringComparer.OrdinalIgnoreCase) { "EQUITY", "ETF", "CRYPTOCURRENCY", "CURRENCY", "FUTURE", "INDEX" };

    private readonly HttpClient _http;
    private readonly ILogger<YahooSymbolSearchClient> _logger;

    public YahooSymbolSearchClient(HttpClient http, ILogger<YahooSymbolSearchClient> logger)
    {
        _http = http;
        _logger = logger;
        // Yahoo blocks requests without a browser-like User-Agent
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<SymbolSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var url = "https://query1.finance.yahoo.com/v1/finance/search"
                + $"?q={Uri.EscapeDataString(query)}&quotesCount=8&newsCount=0&listsCount=0";

        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);

            if (!response.TryGetProperty("quotes", out var quotes))
                return [];

            var results = new List<SymbolSearchResultDto>();

            foreach (var quote in quotes.EnumerateArray())
            {
                var symbol = quote.TryGetProperty("symbol", out var s) ? s.GetString() : null;
                var type = quote.TryGetProperty("quoteType", out var t) ? t.GetString() : null;

                if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(type)) continue;
                if (!SupportedTypes.Contains(type)) continue;

                var name = (quote.TryGetProperty("shortname", out var sn) ? sn.GetString() : null)
                    ?? (quote.TryGetProperty("longname", out var ln) ? ln.GetString() : null)
                    ?? symbol;

                var exchange = quote.TryGetProperty("exchange", out var ex) ? ex.GetString() ?? "" : "";

                results.Add(new SymbolSearchResultDto(
                    Symbol: NormalizeForSystem(symbol, type),
                    Name: name,
                    Type: FriendlyType(type),
                    Exchange: exchange));
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Symbol search failed for query {Query}", query);
            return [];
        }
    }

    // Map Yahoo's symbol formats onto the ones our ingestion pipeline expects.
    // Major cryptos come back as "BTC-USD" — our crypto pipeline uses Binance
    // "BTCUSDT". Anything without a known alias keeps Yahoo's symbol, which
    // our Yahoo candle client can ingest directly.
    private static string NormalizeForSystem(string symbol, string quoteType)
    {
        if (quoteType.Equals("CRYPTOCURRENCY", StringComparison.OrdinalIgnoreCase) &&
            symbol.EndsWith("-USD", StringComparison.OrdinalIgnoreCase))
        {
            var baseCoin = symbol[..^4];
            var normalized = SymbolNormalizer.Normalize(baseCoin);
            if (normalized.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                return normalized;
        }

        return symbol;
    }

    private static string FriendlyType(string quoteType) => quoteType.ToUpperInvariant() switch
    {
        "EQUITY"         => "Stock",
        "ETF"            => "ETF",
        "CRYPTOCURRENCY" => "Crypto",
        "CURRENCY"       => "Forex",
        "FUTURE"         => "Future",
        "INDEX"          => "Index",
        _                => quoteType
    };
}
