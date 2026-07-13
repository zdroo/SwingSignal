using RegimeDeck.Domain.Enums;

namespace RegimeDeck.Domain.Entities;

public class Candle : BaseEntity
{
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = default!;

    public DateTime OpenTime { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public CandleInterval Interval { get; set; }
}
