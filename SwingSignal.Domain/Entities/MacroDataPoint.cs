using SwingSignal.Domain.Enums;

namespace SwingSignal.Domain.Entities;

public class MacroDataPoint : BaseEntity
{
    public MacroIndicatorType IndicatorType { get; set; }
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string Source { get; set; } = default!;
}
