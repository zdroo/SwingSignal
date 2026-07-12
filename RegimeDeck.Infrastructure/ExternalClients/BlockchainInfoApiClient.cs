using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RegimeDeck.Infrastructure.ExternalClients;

/// blockchain.info charts API — free, keyless Bitcoin network metrics
/// (miner revenue, hash rate) with history back to 2009.
public class BlockchainInfoApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BlockchainInfoApiClient> _logger;

    public BlockchainInfoApiClient(HttpClient http, ILogger<BlockchainInfoApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// Full daily series of a chart (e.g. "miners-revenue", "hash-rate"), oldest first.
    public async Task<List<(DateTime Date, decimal Value)>> GetChartAsync(
        string chart, CancellationToken ct = default)
    {
        var url = $"https://api.blockchain.info/charts/{chart}?timespan=all&format=json&sampled=false";
        var points = new List<(DateTime, decimal)>();

        try
        {
            using var response = await _http.GetAsync(new Uri(url), ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            foreach (var row in doc.RootElement.GetProperty("values").EnumerateArray())
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(row.GetProperty("x").GetInt64()).UtcDateTime.Date;
                var value = (decimal)row.GetProperty("y").GetDouble();
                if (value > 0) points.Add((date, value));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "blockchain.info chart fetch failed for {Chart}", chart);
            return [];
        }

        return points;
    }
}
