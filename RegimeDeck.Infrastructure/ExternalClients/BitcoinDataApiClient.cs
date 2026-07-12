using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RegimeDeck.Infrastructure.ExternalClients;

/// bitcoin-data.com — free, keyless on-chain metrics. Currently the only
/// free source of MVRV (CoinMetrics community and blockchain.info both
/// dropped it); if it ever disappears, the fallback is the Mayer Multiple
/// computed from our own candles.
public class BitcoinDataApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BitcoinDataApiClient> _logger;

    public BitcoinDataApiClient(HttpClient http, ILogger<BitcoinDataApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// Daily MVRV (market cap / realized cap) between two dates, oldest
    /// first. The API silently truncates long ranges (~4 years), so the
    /// range is fetched in one-year chunks.
    public async Task<List<(DateTime Date, decimal Value)>> GetMvrvAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var points = new List<(DateTime, decimal)>();

        try
        {
            for (var cursor = from; cursor <= to; cursor = cursor.AddDays(366))
            {
                var end = cursor.AddDays(365) < to ? cursor.AddDays(365) : to;
                var url = $"https://bitcoin-data.com/v1/mvrv?startday={cursor:yyyy-MM-dd}&endday={end:yyyy-MM-dd}";

                using var response = await _http.GetAsync(new Uri(url), ct);
                response.EnsureSuccessStatusCode();

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    var date = DateTime.ParseExact(
                        row.GetProperty("d").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    var value = row.GetProperty("mvrv").GetDecimal();
                    if (value > 0) points.Add((date, value));
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "bitcoin-data.com MVRV fetch failed");
            return points.Count > 0 ? points : [];
        }

        return points;
    }
}
