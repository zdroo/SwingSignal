namespace RegimeDeck.Contracts.Sectors;

/// One sector's rotation read: regime fit (odds/edge/stance for its ETF)
/// plus momentum (relative strength vs the broad market).
public record SectorRotationRowDto(
    string Symbol,
    string Sector,
    double? Odds3M,
    double? Edge3M,
    string? Stance,
    double? RelStrength3M);

public record SectorRotationResultDto(
    List<SectorRotationRowDto> Sectors,
    string Benchmark,     // what relative strength is measured against, e.g. SPY
    DateTime? AsOf);
