using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Domain.Entities;

public class Asset : BaseEntity
{
    public string Symbol { get; set; } = default!;
    public string Name { get; set; } = default!;
    public MarketType MarketType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Candle> Candles { get; set; } = [];
}
