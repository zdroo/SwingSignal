using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.ExternalClients;

public class BinanceApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BinanceApiClient> _logger;

    private static readonly Dictionary<CandleInterval, string> IntervalMap = new()
    {
        [CandleInterval.OneHour]  = "1h",
        [CandleInterval.FourHour] = "4h",
        [CandleInterval.OneDay]   = "1d",
        [CandleInterval.OneWeek]  = "1w",
    };

    public BinanceApiClient(HttpClient http, ILogger<BinanceApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<RawCandle>> GetCandlesAsync(
        string symbol,
        CandleInterval interval,
        DateTime? from = null,
        int limit = 1000,
        CancellationToken ct = default)
    {
        var intervalStr = IntervalMap[interval];
        var url = $"https://api.binance.com/api/v3/klines?symbol={symbol}&interval={intervalStr}&limit={limit}";

        if (from.HasValue)
        {
            var startMs = new DateTimeOffset(from.Value, TimeSpan.Zero).ToUnixTimeMilliseconds();
            url += $"&startTime={startMs}";
        }

        try
        {
            var raw = await _http.GetFromJsonAsync<JsonElement[][]>(url, ct);

            return raw?.Select(k => new RawCandle(
                OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(k[0].GetInt64()).UtcDateTime,
                Open:     decimal.Parse(k[1].GetString()!),
                High:     decimal.Parse(k[2].GetString()!),
                Low:      decimal.Parse(k[3].GetString()!),
                Close:    decimal.Parse(k[4].GetString()!),
                Volume:   decimal.Parse(k[5].GetString()!)
            )).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Binance candles for {Symbol}", symbol);
            return [];
        }
    }
}

public record RawCandle(DateTime OpenTime, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);
