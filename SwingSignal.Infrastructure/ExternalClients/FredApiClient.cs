using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwingSignal.Domain.Enums;

namespace SwingSignal.Infrastructure.ExternalClients;

public class FredApiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<FredApiClient> _logger;

    private static readonly Dictionary<MacroIndicatorType, string> SeriesIds = new()
    {
        [MacroIndicatorType.FedFundsRate]      = "FEDFUNDS",
        [MacroIndicatorType.UnemploymentRate]  = "UNRATE",
        [MacroIndicatorType.CPI]               = "CPIAUCSL",
        [MacroIndicatorType.GDP]               = "GDP",
        [MacroIndicatorType.GoldPrice]         = "GOLDAMGBD228NLBM",
        [MacroIndicatorType.OilWTI]            = "DCOILWTICO",
        [MacroIndicatorType.TreasuryYield10Y]  = "DGS10",
        [MacroIndicatorType.TreasuryYield2Y]   = "DGS2",
    };

    public FredApiClient(HttpClient http, IConfiguration config, ILogger<FredApiClient> logger)
    {
        _http = http;
        _apiKey = config["Fred:ApiKey"] ?? throw new InvalidOperationException("Fred:ApiKey not configured");
        _logger = logger;
    }

    public async Task<List<(DateTime Date, decimal Value)>> GetObservationsAsync(
        MacroIndicatorType type,
        DateTime? from = null,
        CancellationToken ct = default)
    {
        if (!SeriesIds.TryGetValue(type, out var seriesId))
            return [];

        var startDate = (from ?? DateTime.UtcNow.AddYears(-5)).ToString("yyyy-MM-dd");
        var url = $"https://api.stlouisfed.org/fred/series/observations"
                + $"?series_id={seriesId}"
                + $"&api_key={_apiKey}"
                + $"&file_type=json"
                + $"&observation_start={startDate}"
                + $"&sort_order=asc";

        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
            var observations = response.GetProperty("observations");

            var result = new List<(DateTime, decimal)>();

            foreach (var obs in observations.EnumerateArray())
            {
                var dateStr = obs.GetProperty("date").GetString();
                var valueStr = obs.GetProperty("value").GetString();

                // FRED returns "." for missing values
                if (valueStr is null or ".")
                    continue;

                if (DateTime.TryParse(dateStr, out var date) && decimal.TryParse(valueStr, out var value))
                    result.Add((date, value));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FRED series {SeriesId}", seriesId);
            return [];
        }
    }
}
