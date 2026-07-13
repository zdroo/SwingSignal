using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Infrastructure.ExternalClients;

// Keyless fallback for core macro series when no FRED API key is configured.
// DBnomics (db.nomics.world) mirrors Fed H.15, BLS and BEA data for free.
// Coverage is a subset of FRED — indicators not listed here are simply skipped.
public class DbNomicsApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<DbNomicsApiClient> _logger;

    private static readonly Dictionary<MacroIndicatorType, string> SeriesIds = new()
    {
        [MacroIndicatorType.FedFundsRate]     = "FED/H15/RIFSPFF_N.M",
        [MacroIndicatorType.TreasuryYield10Y] = "FED/H15/RIFLGFCY10_N.M",
        [MacroIndicatorType.TreasuryYield2Y]  = "FED/H15/RIFLGFCY02_N.M",
        [MacroIndicatorType.TreasuryYield3M]  = "FED/H15/RIFLGFCM03_N.M",
        [MacroIndicatorType.RealYield10Y]     = "FED/H15/RIFLGFCY10_XII_N.M",
        [MacroIndicatorType.CPI]              = "BLS/cu/CUSR0000SA0",
        [MacroIndicatorType.UnemploymentRate] = "BLS/ln/LNS14000000",
        [MacroIndicatorType.GDP]              = "BEA/NIPA-T10106/A191RX-Q",
    };

    public DbNomicsApiClient(HttpClient http, ILogger<DbNomicsApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public static bool Supports(MacroIndicatorType type) => SeriesIds.ContainsKey(type);

    public async Task<List<(DateTime Date, decimal Value)>> GetObservationsAsync(
        MacroIndicatorType type,
        DateTime? from = null,
        CancellationToken ct = default)
    {
        if (!SeriesIds.TryGetValue(type, out var seriesId))
            return [];

        var url = $"https://api.db.nomics.world/v22/series/{seriesId}?observations=1&format=json";

        try
        {
            var response = await _http.GetFromJsonAsync<JsonElement>(url, ct);
            var doc = response.GetProperty("series").GetProperty("docs")[0];

            var periods = doc.GetProperty("period").EnumerateArray().Select(p => p.GetString()).ToArray();
            var values = doc.GetProperty("value").EnumerateArray().ToArray();

            var result = new List<(DateTime, decimal)>();
            var cutoff = from ?? DateTime.MinValue;

            for (var i = 0; i < periods.Length; i++)
            {
                if (values[i].ValueKind != JsonValueKind.Number) continue;

                var date = ParsePeriod(periods[i]);
                if (date is null || date < cutoff) continue;

                result.Add((date.Value, (decimal)values[i].GetDouble()));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch DBnomics series {SeriesId}", seriesId);
            return [];
        }
    }

    // DBnomics period formats: "2024-05-31" (daily), "2024-05" (monthly), "2024-Q2" (quarterly)
    private static DateTime? ParsePeriod(string? period)
    {
        if (string.IsNullOrEmpty(period)) return null;

        if (period.Contains('Q'))
        {
            var parts = period.Split("-Q");
            if (parts.Length == 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var quarter))
                return new DateTime(year, (quarter - 1) * 3 + 1, 1);
            return null;
        }

        if (period.Length == 7 && DateTime.TryParse(period + "-01", out var monthly))
            return monthly;

        return DateTime.TryParse(period, out var daily) ? daily : null;
    }
}
