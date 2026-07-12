namespace RegimeDeck.Contracts.Assets;

public record SymbolSearchResultDto(
    string Symbol,   // the symbol our system understands (already normalized)
    string Name,
    string Type,     // Stock | ETF | Crypto | Forex | Future | Index
    string Exchange);
