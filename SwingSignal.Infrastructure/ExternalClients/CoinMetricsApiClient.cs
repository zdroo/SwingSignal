using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SwingSignal.Infrastructure.ExternalClients;

/// CoinMetrics Community API — free, keyless daily reference rates reaching
/// years before any Binance listing (BTC from 2010, ETH from 2015). Used only
/// for the one-time pre-Binance backfill. Reference rates are closes only, so
/// candles come back synthetic (O=H=L=C, volume 0) — sufficient because the
/// odds engine reads closes exclusively.
public class CoinMetricsApiClient
{
    private const int PageSize = 10_000; // ~27 years of dailies per page
    private const int MaxPages = 5;

    private readonly HttpClient _http;
    private readonly ILogger<CoinMetricsApiClient> _logger;

    public CoinMetricsApiClient(HttpClient http, ILogger<CoinMetricsApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// Daily closes for a coin between two dates, oldest first.
    public async Task<List<RawCandle>> GetDailyClosesAsync(
        string coin, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var series = await GetMetricSeriesAsync(coin, "PriceUSD", from, to, ct);
        return series
            .Select(p => new RawCandle(p.Date, p.Value, p.Value, p.Value, p.Value, Volume: 0))
            .ToList();
    }

    /// Any community-tier daily metric (PriceUSD, CapMrktCurUSD, SplyCur, ...)
    /// for an asset between two dates, oldest first. Zero/negative values are skipped.
    public async Task<List<(DateTime Date, decimal Value)>> GetMetricSeriesAsync(
        string coin, string metric, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var url = "https://community-api.coinmetrics.io/v4/timeseries/asset-metrics" +
                  $"?assets={coin.ToLowerInvariant()}&metrics={metric}&frequency=1d" +
                  $"&paging_from=start&page_size={PageSize}" +
                  $"&start_time={from:yyyy-MM-dd}&end_time={to:yyyy-MM-dd}";

        var points = new List<(DateTime, decimal)>();

        try
        {
            for (var page = 0; page < MaxPages && url is not null; page++)
            {
                using var response = await _http.GetAsync(new Uri(url), ct);
                response.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = doc.RootElement;

                foreach (var row in root.GetProperty("data").EnumerateArray())
                {
                    var value = decimal.Parse(row.GetProperty(metric).GetString()!, CultureInfo.InvariantCulture);
                    if (value <= 0) continue;

                    var time = DateTime.Parse(
                        row.GetProperty("time").GetString()!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal);

                    points.Add((time, value));
                }

                url = root.TryGetProperty("next_page_url", out var next) ? next.GetString() : null;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "CoinMetrics {Metric} fetch failed for {Coin}", metric, coin);
            return [];
        }

        return points;
    }
}
