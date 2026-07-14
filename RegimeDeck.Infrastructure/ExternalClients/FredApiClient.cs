using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RegimeDeck.Domain.Enums;

using System.Globalization;

namespace RegimeDeck.Infrastructure.ExternalClients;

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
        // GoldPrice moved to Yahoo (GC=F) — FRED's LBMA series was discontinued
        [MacroIndicatorType.OilWTI]            = "DCOILWTICO",
        [MacroIndicatorType.TreasuryYield10Y]  = "DGS10",
        [MacroIndicatorType.TreasuryYield2Y]   = "DGS2",
        [MacroIndicatorType.TreasuryYield3M]   = "DGS3MO",
        [MacroIndicatorType.FedBalanceSheet]   = "WALCL",
        [MacroIndicatorType.ReverseRepo]       = "RRPONTSYD",
        [MacroIndicatorType.RealYield10Y]      = "DFII10",
        [MacroIndicatorType.M2MoneySupply]     = "M2SL",
        [MacroIndicatorType.CorePCE]           = "PCEPILFE",
        [MacroIndicatorType.JoblessClaims]     = "ICSA",
        [MacroIndicatorType.ConsumerSentiment] = "UMCSENT",
        [MacroIndicatorType.RetailSales]       = "RSAFS",
        [MacroIndicatorType.HousingStarts]     = "HOUST",
        [MacroIndicatorType.HighYieldSpread]   = "BAMLH0A0HYM2",
        [MacroIndicatorType.SahmRule]          = "SAHMREALTIME",
    };

    public FredApiClient(HttpClient http, IConfiguration config, ILogger<FredApiClient> logger)
    {
        _http = http;
        _apiKey = config["Fred:ApiKey"] ?? "";
        _logger = logger;
    }

    // False when the key is missing or still the placeholder — callers fall back to DBnomics
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

    public async Task<List<(DateTime Date, decimal Value)>> GetObservationsAsync(
        MacroIndicatorType type,
        DateTime? from = null,
        CancellationToken ct = default)
    {
        if (!SeriesIds.TryGetValue(type, out var seriesId))
            return [];

        var startDate = (from ?? DateTime.UtcNow.AddYears(-5)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
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

    /// Future scheduled dates for a FRED release (its publication calendar),
    /// on or after `from`. Fail-soft: an empty list on any error, so the
    /// event calendar degrades to "no events" rather than throwing.
    public async Task<List<DateTime>> GetReleaseDatesAsync(
        int releaseId, DateTime from, CancellationToken ct = default)
    {
        var startDate = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"https://api.stlouisfed.org/fred/release/dates"
                + $"?release_id={releaseId}"
                + $"&api_key={_apiKey}"
                + $"&file_type=json"
                + $"&include_release_dates_with_no_data=true" // includes not-yet-published dates
                + $"&sort_order=asc";

        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
            if (!response.TryGetProperty("release_dates", out var dates))
                return [];

            var result = new List<DateTime>();
            foreach (var d in dates.EnumerateArray())
            {
                if (DateTime.TryParse(d.GetProperty("date").GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date) && date >= from.Date)
                    result.Add(date.Date);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FRED release dates for {ReleaseId}", releaseId);
            return [];
        }
    }
}
