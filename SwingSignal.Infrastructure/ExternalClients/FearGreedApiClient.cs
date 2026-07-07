using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SwingSignal.Infrastructure.ExternalClients;

// Alternative.me Crypto Fear & Greed Index — free, no API key, history since Feb 2018
public class FearGreedApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FearGreedApiClient> _logger;

    public FearGreedApiClient(HttpClient http, ILogger<FearGreedApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<(DateTime Date, decimal Value)>> GetHistoryAsync(CancellationToken ct = default)
    {
        // limit=0 returns the full history
        const string url = "https://api.alternative.me/fng/?limit=0&format=json";

        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
            var data = response.GetProperty("data");

            var result = new List<(DateTime, decimal)>();

            foreach (var entry in data.EnumerateArray())
            {
                var valueStr = entry.GetProperty("value").GetString();
                var timestampStr = entry.GetProperty("timestamp").GetString();

                if (valueStr is null || timestampStr is null) continue;

                if (decimal.TryParse(valueStr, out var value) && long.TryParse(timestampStr, out var unix))
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.Date;
                    result.Add((date, value));
                }
            }

            return result.OrderBy(r => r.Item1).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Fear & Greed index");
            return [];
        }
    }
}
