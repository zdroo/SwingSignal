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
    YieldCurveSpread,   // 10Y - 2Y, computed on ingest

    // Rates & liquidity (FRED)
    TreasuryYield3M,    // DGS3MO
    YieldSpread10Y3M,   // 10Y - 3M, computed — the Fed's preferred recession signal
    FedBalanceSheet,    // WALCL — QE/QT, the "global liquidity" driver
    ReverseRepo,        // RRPONTSYD — liquidity parked at the Fed
    RealYield10Y,       // DFII10 — 10Y TIPS, gold's strongest inverse correlation
    M2MoneySupply,      // M2SL

    // Economy (FRED)
    CorePCE,            // PCEPILFE — the Fed's actual inflation target
    JoblessClaims,      // ICSA — weekly, leads the unemployment rate
    ConsumerSentiment,  // UMCSENT
    RetailSales,        // RSAFS
    HousingStarts,      // HOUST — housing leads recessions
    HighYieldSpread,    // BAMLH0A0HYM2 — junk bond stress, early-warning signal
    SahmRule,           // SAHMREALTIME — recession trigger from unemployment momentum

    // Market-derived (Yahoo Finance)
    VIX,                // ^VIX — fear gauge
    DollarIndex,        // DX-Y.NYB — strong dollar crushes crypto/commodities/EM
    Copper,             // HG=F — "Dr. Copper", global growth proxy

    // Sentiment (Alternative.me)
    CryptoFearGreed,    // 0-100, crypto market sentiment

    // Derived momentum dimensions — computed at snapshot time, never ingested.
    // Fed at 5% while hiking is a different regime than Fed at 5% while cutting.
    FedFundsMomentum6M,        // 6-month change of the Fed funds rate
    UnemploymentMomentum6M,    // 6-month change of the unemployment rate
    Yield10YMomentum6M,        // 6-month change of the 10Y yield
    HighYieldSpreadMomentum6M, // 6-month change of HY credit spreads
    CpiMomentum6M,             // 6-month change of CPI YoY (inflation accelerating vs cooling)
}
