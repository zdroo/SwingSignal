using System.Net;

namespace RegimeDeck.Tests.Integration;

// The landing page promises "BTC, SPY and Gold are free without an account" —
// pin the exact symbols that promise covers, through the real HTTP pipeline.
// Regression test for a real bug: GLD (the ticker actually surfaced in the
// popular-symbols list and sitemap) was gated while only GC=F was free.
public class RegimeGateTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public RegimeGateTests(ApiFactory factory)
    {
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("BTCUSDT")]
    [InlineData("SPY")]
    [InlineData("GLD")]
    [InlineData("GC=F")]
    [InlineData("btcusdt")] // case-insensitive
    public async Task FlagshipSymbols_FreeWithoutAccount(string symbol)
    {
        var response = await _client.GetAsync($"/api/regime/odds/{Uri.EscapeDataString(symbol)}");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NonFlagshipSymbol_GatedWithoutAccount()
    {
        var response = await _client.GetAsync("/api/regime/odds/AAPL");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
