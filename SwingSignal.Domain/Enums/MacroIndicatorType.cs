namespace SwingSignal.Domain.Enums;

public enum MacroIndicatorType
{
    FedFundsRate,
    UnemploymentRate,
    CPI,
    GDP,
    GoldPrice,
    OilWTI,
    TreasuryYield10Y,
    TreasuryYield2Y,
    YieldCurveSpread  // 10Y - 2Y, computed on ingest
}
