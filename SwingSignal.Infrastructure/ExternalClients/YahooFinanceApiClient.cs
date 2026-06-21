using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.ExternalClients;

public class YahooFinanceApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceApiClient> _logger;

    public YahooFinanceApiClient(HttpClient http, ILogger<YahooFinanceApiClient> logger)
    {
        _http = http;
        _logger = logger;
        // Yahoo Finance blocks requests without a browser-like User-Agent
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<RawCandle>> GetCandlesAsync(
        string symbol,
        CandleInterval interval,
        DateTime? from = null,
        CancellationToken ct = default)
    {
        var intervalStr = ToYahooInterval(interval);
        var period1 = ToUnixTimestamp(from ?? DateTime.UtcNow.AddYears(-5));
        var period2 = ToUnixTimestamp(DateTime.UtcNow);

        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}"
                + $"?interval={intervalStr}&period1={period1}&period2={period2}";

        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);

            var result = response
                .GetProperty("chart")
                .GetProperty("result")[0];

            var timestamps = result
                .GetProperty("timestamp")
                .EnumerateArray()
                .Select(t => DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()).UtcDateTime)
                .ToArray();

            var quote = result
                .GetProperty("indicators")
                .GetProperty("quote")[0];

            var opens   = ParseDecimalArray(quote.GetProperty("open"));
            var highs   = ParseDecimalArray(quote.GetProperty("high"));
            var lows    = ParseDecimalArray(quote.GetProperty("low"));
            var closes  = ParseDecimalArray(quote.GetProperty("close"));
            var volumes = ParseDecimalArray(quote.GetProperty("volume"));

            var candles = new List<RawCandle>();

            for (int i = 0; i < timestamps.Length; i++)
            {
                // Skip rows with missing OHLC data
                if (closes[i] is null || opens[i] is null) continue;

                candles.Add(new RawCandle(
                    OpenTime: timestamps[i],
                    Open:     opens[i]!.Value,
                    High:     highs[i] ?? opens[i]!.Value,
                    Low:      lows[i]  ?? opens[i]!.Value,
                    Close:    closes[i]!.Value,
                    Volume:   volumes[i] ?? 0m
                ));
            }

            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Yahoo Finance candles for {Symbol}", symbol);
            return [];
        }
    }

    private static decimal?[] ParseDecimalArray(JsonElement array)
    {
        return array.EnumerateArray()
            .Select(e => e.ValueKind == JsonValueKind.Null ? (decimal?)null : (decimal?)e.GetDouble())
            .ToArray();
    }

    private static string ToYahooInterval(CandleInterval interval) => interval switch
    {
        CandleInterval.OneHour  => "1h",
        CandleInterval.FourHour => "60m",  // Yahoo doesn't have 4h — use 1h as fallback
        CandleInterval.OneDay   => "1d",
        CandleInterval.OneWeek  => "1wk",
        _                       => "1d"
    };

    private static long ToUnixTimestamp(DateTime dt) =>
        new DateTimeOffset(dt, TimeSpan.Zero).ToUnixTimeSeconds();
}
