using RegimeDeck.Application.Regime;
using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Tests.Regime;

// The regime-playbook board is built by running the SAME RegimeInsight engine
// over a catalog of canonical regimes, then matching today's live readings to
// the nearest. These lock both the catalog's intent (each regime favors what
// textbook macro says it should) and the matching contract.
public class RegimePlaybookTests
{
    private static RegimePlaybookService Service(IMacroRegimeService regime) => new(regime);

    private static IReadOnlyDictionary<string, string> Signals(string id) =>
        RegimePlaybookCatalog.All.First(a => a.Id == id).Signals;

    // A fake "current regime" whose Health is computed from a signal set, so we
    // can point today's readings at any archetype (or at nothing).
    private static FakeRegime CurrentFrom(IReadOnlyDictionary<string, string> signals)
    {
        var health = RegimeInsight.ComputeMarketHealth(signals);
        var playbook = RegimeInsight.ComputePlaybook(health, signals);
        var dto = new MacroRegimeDto([], health, [], playbook, DateTime.UtcNow);
        return new FakeRegime(dto);
    }

    private static PlaybookDto PlaybookFor(RegimePlaybookBoardDto board, string id) =>
        board.Regimes.First(r => r.Id == id).Playbook;

    // ── Catalog invariants ──────────────────────────────────────────────────

    [Fact]
    public void Catalog_HasSixRegimes_WithUniqueIdsAndHallmarks()
    {
        var all = RegimePlaybookCatalog.All;

        Assert.Equal(6, all.Count);
        Assert.Equal(all.Count, all.Select(a => a.Id).Distinct().Count());
        Assert.All(all, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.NotEmpty(a.Hallmarks);
            // Every archetype must populate all six health groups so matching
            // compares like-for-like against the live regime.
            Assert.Equal(6, RegimeInsight.ComputeMarketHealth(a.Signals).Groups.Count);
        });
    }

    // ── Board shape ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Board_ReturnsEveryRegime_InCatalogOrder()
    {
        var board = await Service(CurrentFrom(Signals("risk-on-expansion"))).GetBoardAsync();

        Assert.Equal(
            RegimePlaybookCatalog.All.Select(a => a.Id),
            board.Regimes.Select(r => r.Id));
        Assert.All(board.Regimes, r => Assert.InRange(r.MatchScore, 0, 100));
        Assert.All(board.Regimes, r => Assert.Equal(5, r.Playbook.Assets.Count));
    }

    [Fact]
    public async Task Board_UnclassifiableCurrent_FlagsNothing()
    {
        // Ingestion still warming: no indicators → no health groups → no match
        var board = await Service(CurrentFrom(new Dictionary<string, string>())).GetBoardAsync();

        Assert.Null(board.CurrentRegimeId);
        Assert.Null(board.RunnerUpRegimeId);
        Assert.All(board.Regimes, r => Assert.False(r.IsCurrent));
        Assert.All(board.Regimes, r => Assert.Equal(0, r.MatchScore));
    }

    [Fact]
    public async Task Board_CurrentMatchingAnArchetype_FlagsItCurrentWithPerfectScore()
    {
        // Today's readings are exactly the risk-off archetype → identical groups
        var board = await Service(CurrentFrom(Signals("risk-off-recession"))).GetBoardAsync();

        Assert.Equal("risk-off-recession", board.CurrentRegimeId);
        var current = board.Regimes.Single(r => r.IsCurrent);
        Assert.Equal("risk-off-recession", current.Id);
        Assert.Equal(100, current.MatchScore);

        Assert.NotNull(board.RunnerUpRegimeId);
        Assert.NotEqual(board.CurrentRegimeId, board.RunnerUpRegimeId);
    }

    [Fact]
    public async Task Board_MarksExactlyOneCurrent_WhenClassifiable()
    {
        var board = await Service(CurrentFrom(Signals("stagflation"))).GetBoardAsync();

        Assert.Single(board.Regimes, r => r.IsCurrent);
    }

    // ── Catalog intent: each regime favors what textbook macro says ──────────

    [Fact]
    public async Task RiskOnExpansion_FavorsStocksFirst()
    {
        var board = await Service(CurrentFrom(Signals("risk-on-expansion"))).GetBoardAsync();
        var playbook = PlaybookFor(board, "risk-on-expansion");

        Assert.Equal("Stocks", playbook.Assets[0].Name);
        Assert.Equal("Favored", playbook.Assets[0].Verdict);
    }

    [Fact]
    public async Task ReflationRecovery_FavorsCryptoFirst()
    {
        var board = await Service(CurrentFrom(Signals("reflation-recovery"))).GetBoardAsync();
        var playbook = PlaybookFor(board, "reflation-recovery");

        Assert.Equal("Crypto (majors)", playbook.Assets[0].Name);
    }

    [Fact]
    public async Task Stagflation_RanksGoldAboveStocksAndBonds()
    {
        var board = await Service(CurrentFrom(Signals("stagflation"))).GetBoardAsync();
        var names = PlaybookFor(board, "stagflation").Assets.Select(a => a.Name).ToList();

        Assert.True(names.IndexOf("Gold") < names.IndexOf("Stocks"));
        Assert.True(names.IndexOf("Gold") < names.IndexOf("Long-Term Bonds"));
    }

    [Fact]
    public async Task Overheating_RanksGoldAboveStocks_AndCryptoFacesHeadwinds()
    {
        var board = await Service(CurrentFrom(Signals("overheating-late-cycle"))).GetBoardAsync();
        var assets = PlaybookFor(board, "overheating-late-cycle").Assets;
        var names = assets.Select(a => a.Name).ToList();

        Assert.True(names.IndexOf("Gold") < names.IndexOf("Stocks"));
        Assert.Equal("Headwinds", assets.Single(a => a.Name == "Crypto (majors)").Verdict);
    }

    [Fact]
    public async Task TighteningDisinflation_FavorsCashFirst()
    {
        var board = await Service(CurrentFrom(Signals("tightening-disinflation"))).GetBoardAsync();
        var playbook = PlaybookFor(board, "tightening-disinflation");

        Assert.Equal("Cash & T-Bills", playbook.Assets[0].Name);
    }

    [Fact]
    public async Task RiskOffRecession_FavorsCashFirst_AndPunishesRiskAssets()
    {
        var board = await Service(CurrentFrom(Signals("risk-off-recession"))).GetBoardAsync();
        var assets = PlaybookFor(board, "risk-off-recession").Assets;

        Assert.Equal("Cash & T-Bills", assets[0].Name);
        Assert.Equal("Headwinds", assets.Single(a => a.Name == "Stocks").Verdict);
        Assert.Equal("Headwinds", assets.Single(a => a.Name == "Crypto (majors)").Verdict);
    }

    private sealed class FakeRegime : IMacroRegimeService
    {
        private readonly MacroRegimeDto _dto;
        public FakeRegime(MacroRegimeDto dto) => _dto = dto;

        public Task<MacroRegimeDto> GetCurrentRegimeAsync(CancellationToken ct = default) =>
            Task.FromResult(_dto);

        public Task<List<HistoricalMatchDto>> FindSimilarPeriodsAsync(
            int topK = 10, MatchingOptions? options = null,
            DateTime? minAnalogDate = null, CancellationToken ct = default) =>
            throw new NotSupportedException("Not used by the playbook board");
    }
}
