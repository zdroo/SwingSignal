using RegimeDeck.Contracts.Regime;

namespace RegimeDeck.Application.Regime;

public class RegimePlaybookService : IRegimePlaybookService
{
    private const string Note =
        "Each regime shows what its conditions have historically favored — the same rule-based " +
        "engine that reads today's macro, applied to a textbook version of that regime. The current " +
        "regime is the one today's readings sit closest to; real markets blend regimes, so treat the " +
        "match as a lean, not a label. Descriptive, not financial advice.";

    private readonly IMacroRegimeService _regime;

    public RegimePlaybookService(IMacroRegimeService regime) => _regime = regime;

    public async Task<RegimePlaybookBoardDto> GetBoardAsync(CancellationToken ct = default)
    {
        var current = await _regime.GetCurrentRegimeAsync(ct);
        var currentGroups = current.Health.Groups.ToDictionary(g => g.Name, g => g.Score);
        var classifiable = currentGroups.Count > 0;

        var scored = RegimePlaybookCatalog.All
            .Select(a =>
            {
                var health = RegimeInsight.ComputeMarketHealth(a.Signals);
                var playbook = RegimeInsight.ComputePlaybook(health, a.Signals);
                var archetypeGroups = health.Groups.ToDictionary(g => g.Name, g => g.Score);
                var match = classifiable ? MatchScore(currentGroups, archetypeGroups) : 0;
                return (Archetype: a, Health: health, Playbook: playbook, Match: match);
            })
            .ToList();

        // Nearest wins; ties keep catalog order (OrderByDescending is stable).
        var ranked = classifiable
            ? scored.OrderByDescending(x => x.Match).ToList()
            : [];
        var currentId = ranked.Count > 0 ? ranked[0].Archetype.Id : null;
        var runnerUpId = ranked.Count > 1 ? ranked[1].Archetype.Id : null;

        var regimes = scored
            .Select(x => new RegimePlaybookDto(
                x.Archetype.Id,
                x.Archetype.Name,
                x.Archetype.Summary,
                [.. x.Archetype.Hallmarks],
                x.Health.Score,
                x.Health.Label,
                x.Playbook,
                IsCurrent: x.Archetype.Id == currentId,
                MatchScore: x.Match))
            .ToList();

        return new RegimePlaybookBoardDto(regimes, currentId, runnerUpId, Note);
    }

    // Similarity of two regimes on a 0-100 scale: RMS distance across the health
    // groups they share (each group score is already 0-100), flipped so 100 = identical.
    private static int MatchScore(
        Dictionary<string, int> current, Dictionary<string, int> archetype)
    {
        var shared = current.Keys.Where(archetype.ContainsKey).ToList();
        if (shared.Count == 0) return 0;

        var meanSquared = shared.Average(k => Math.Pow(current[k] - archetype[k], 2));
        var distance = Math.Sqrt(meanSquared);
        return (int)Math.Round(Math.Clamp(100 - distance, 0, 100), MidpointRounding.AwayFromZero);
    }
}
