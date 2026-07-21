namespace RegimeDeck.Contracts.Assets;

public record AssetDto(Guid Id, string Symbol, string Name, string MarketType, bool IsActive);
