// Single source of truth for indicator education content.
// Threshold bands must stay in sync with SignalClassifier on the backend.

export interface IndicatorInfo {
  key: string;
  name: string;
  unit: string;
  family: string;
  short: string;  // one-liner: what it is (tooltip)
  low: string;    // what a low value means
  high: string;   // what a high value means
  bands: string;  // signal thresholds, mirrors the backend classifier
  detail: string; // longer explanation for the Learn page
}

export const FAMILIES = [
  "Policy & Liquidity",
  "Rates & Yield Curve",
  "Inflation",
  "Labor Market",
  "Growth & Consumer",
  "Market Stress & Sentiment",
  "Commodities",
] as const;

export const INDICATORS: IndicatorInfo[] = [
  // ── Policy & Liquidity ────────────────────────────────────────────────
  {
    key: "FedFundsRate",
    name: "Fed Funds Rate",
    unit: "%",
    family: "Policy & Liquidity",
    short: "The interest rate US banks charge each other overnight — the Fed's main lever for the whole economy.",
    low: "Cheap money: borrowing is easy, liquidity flows into risk assets. Historically the strongest tailwind for stocks and crypto.",
    high: "Expensive money: borrowing slows, safe assets pay well, risk assets face a headwind. Markets often rally 6-12 months before the first cut.",
    bands: "Accommodative < 2% · Neutral 2-4% · Restrictive > 4%",
    detail:
      "Every other interest rate in the economy — mortgages, business loans, bond yields — keys off this rate. When the Fed raises it, it is deliberately cooling the economy to fight inflation; when it cuts, it is stimulating growth. Markets are forward-looking: the direction and expected path of this rate often matters more than its level, which is why SwingSignal also tracks its 6-month momentum.",
  },
  {
    key: "RealYield10Y",
    name: "10Y Real Yield",
    unit: "%",
    family: "Policy & Liquidity",
    short: "The 10-year Treasury yield after subtracting expected inflation — what savers truly earn.",
    low: "Negative real yields mean cash and bonds lose purchasing power — investors are pushed into stocks, gold, and crypto.",
    high: "Positive real yields mean safe assets genuinely pay. Gold and non-yielding assets suffer their strongest headwind.",
    bands: "Negative (Easy) < 0% · Neutral 0-1.5% · Restrictive > 1.5%",
    detail:
      "This is gold's most important driver: gold pays no interest, so when inflation-protected bonds pay a solid real return, holding gold has a real opportunity cost. Deeply negative real yields (2020-2021) coincided with all-time highs in gold and crypto; sharply positive real yields historically pressure both.",
  },
  {
    key: "FedBalanceSheet",
    name: "Fed Balance Sheet",
    unit: "% YoY",
    family: "Policy & Liquidity",
    short: "How fast the Fed is printing (QE) or draining (QT) money by buying or selling bonds.",
    low: "Contraction (QT) drains liquidity from markets — a persistent headwind. The 2018 and 2022 drawdowns both happened during QT.",
    high: "Expansion (QE) injects liquidity — historically the single strongest tailwind for risk assets, crypto especially (2020-21).",
    bands: "QT < -2% YoY · Stable -2% to +5% · QE > +5% YoY",
    detail:
      "When the Fed buys bonds it pays with newly created reserves — that money flows through banks into markets. Asset prices, especially the most speculative ones, have tracked the direction of the balance sheet remarkably closely since 2009. Watching the year-over-year change tells you whether the liquidity tide is rising or falling.",
  },
  {
    key: "M2MoneySupply",
    name: "M2 Money Supply",
    unit: "% YoY",
    family: "Policy & Liquidity",
    short: "The total amount of money in the economy — cash, checking and savings accounts.",
    low: "Contracting M2 is rare and deflationary — less money chasing assets. It preceded the 2022-23 slowdown.",
    high: "Fast growth (like +25% in 2020) means money is being created rapidly — it eventually shows up in asset prices and inflation.",
    bands: "Contracting < 0% · Normal 0-10% · Expanding Fast > 10% YoY",
    detail:
      "M2 growth is the slow-moving backdrop of everything: the 2020 money-supply explosion preceded both the 2021 asset melt-up and the 2022 inflation wave. Its first contraction in decades preceded the disinflation that followed. It moves slowly, which makes it a regime indicator rather than a timing tool.",
  },
  {
    key: "ReverseRepo",
    name: "Reverse Repo",
    unit: "$B",
    family: "Policy & Liquidity",
    short: "Money that banks and funds park overnight at the Fed instead of investing it.",
    low: "Near zero means idle cash has been deployed — the liquidity buffer is spent, markets run on fundamentals.",
    high: "Trillions parked at the Fed is a huge reservoir of sidelined cash; when it drains back into markets it acts like stealth QE.",
    bands: "Low < $100B · Moderate $100-500B · High > $500B",
    detail:
      "Think of it as the market's cash-on-the-sidelines gauge at the institutional level. In 2021-22 over $2 trillion sat here; its steady drawdown through 2023-24 offset much of the Fed's official QT — one reason markets held up better than the tightening suggested.",
  },

  // ── Rates & Yield Curve ───────────────────────────────────────────────
  {
    key: "TreasuryYield10Y",
    name: "10Y Treasury",
    unit: "%",
    family: "Rates & Yield Curve",
    short: "What the US government pays to borrow for 10 years — the benchmark for all long-term rates.",
    low: "Cheap long-term money: mortgages and corporate borrowing are easy, and stocks' future earnings are worth more today.",
    high: "Expensive long-term money: pressure on housing, growth stocks, and anything valued on far-future earnings.",
    bands: "Low < 2% · Neutral 2-4% · High > 4%",
    detail:
      "The 10Y yield is the discount rate of the world: when it rises, the present value of future cash flows falls, hitting long-duration assets (tech, growth stocks) hardest. It also reflects the bond market's combined view of growth and inflation over the next decade — rising yields can mean either optimism about growth or fear of inflation, which is why it's read together with inflation data.",
  },
  {
    key: "TreasuryYield2Y",
    name: "2Y Treasury",
    unit: "%",
    family: "Rates & Yield Curve",
    short: "The 2-year government bond yield — the market's bet on where the Fed is heading.",
    low: "Markets expect the Fed to cut rates (or keep them low) — an easing cycle is priced in.",
    high: "Markets expect the Fed to stay tight — restrictive policy is expected to persist.",
    bands: "Low < 2% · Neutral 2-4% · High > 4%",
    detail:
      "The 2Y is the purest market forecast of Fed policy over the next couple of years. When the 2Y falls sharply below the current Fed funds rate, the bond market is screaming that cuts are coming — historically one of the more reliable macro tells.",
  },
  {
    key: "TreasuryYield3M",
    name: "3M Treasury",
    unit: "%",
    family: "Rates & Yield Curve",
    short: "The 3-month government bill yield — tracks current Fed policy almost exactly.",
    low: "Reflects an accommodative Fed right now.",
    high: "Reflects a restrictive Fed right now.",
    bands: "Low < 2% · Neutral 2-4% · High > 4%",
    detail:
      "The 3M bill barely deviates from the Fed funds rate — its value is as the short leg of the 10Y-3M spread, the Fed's own preferred recession indicator.",
  },
  {
    key: "YieldCurveSpread",
    name: "Yield Curve 10Y-2Y",
    unit: "%",
    family: "Rates & Yield Curve",
    short: "Long-term minus short-term rates. Inversion (negative) has preceded every US recession since 1970.",
    low: "Inverted (negative): short rates exceed long rates — the bond market expects the Fed to break something. Classic recession warning.",
    high: "Steep (positive): normal growth expectations. A re-steepening after inversion often marks the actual danger window.",
    bands: "Inverted < 0% · Flat 0-0.5% · Normal > 0.5%",
    detail:
      "Banks borrow short and lend long — an inverted curve makes lending unprofitable, tightening credit for the whole economy. The subtle part: recessions historically begin not during the inversion but after the curve un-inverts, when the Fed starts cutting because the damage is done. Watch the combination of this spread with the Sahm Rule and jobless claims.",
  },
  {
    key: "YieldSpread10Y3M",
    name: "Yield Curve 10Y-3M",
    unit: "%",
    family: "Rates & Yield Curve",
    short: "The Fed's own preferred recession gauge — same logic as 10Y-2Y, using the 3-month bill.",
    low: "Inverted: statistically the strongest single recession predictor in macro, with fewer false alarms than 10Y-2Y.",
    high: "Positive and steep: financing conditions are normal.",
    bands: "Inverted < 0% · Flat 0-0.5% · Normal > 0.5%",
    detail:
      "The New York Fed's recession-probability model is built on this spread. It inverts later and un-inverts later than the 10Y-2Y version, making the two together a useful sequence: 10Y-2Y inverts first (early warning), 10Y-3M confirms (late warning).",
  },

  // ── Inflation ─────────────────────────────────────────────────────────
  {
    key: "CPI",
    name: "CPI Inflation",
    unit: "% YoY",
    family: "Inflation",
    short: "How fast consumer prices are rising versus a year ago — the headline inflation number.",
    low: "Below ~2%: inflation is tame, the Fed has room to cut rates — historically bullish for stocks and bonds.",
    high: "Above ~4%: inflation forces the Fed to stay tight; bonds lose real value; hard assets tend to hold up best.",
    bands: "Low < 2% · Neutral 2-4% · Elevated > 4% YoY",
    detail:
      "Inflation is the villain of every tightening cycle: it is the one problem the Fed will deliberately cause a recession to solve. The direction matters as much as the level — inflation falling from 6% to 4% (disinflation) has historically been a strong environment for risk assets, while inflation re-accelerating from a low base is what keeps central bankers awake.",
  },
  {
    key: "CorePCE",
    name: "Core PCE",
    unit: "% YoY",
    family: "Inflation",
    short: "The Fed's actual inflation target measure — excludes volatile food and energy prices.",
    low: "At or below 2%: mission accomplished for the Fed; policy can ease.",
    high: "Above 3%: the Fed's target is being missed — expect policy to stay restrictive regardless of what headline CPI does.",
    bands: "On Target < 2% · Above Target 2-3% · Elevated > 3% YoY",
    detail:
      "When Fed officials say 'inflation', this is the number they mean. It runs cooler and smoother than CPI. The gap between CPI and Core PCE is itself informative: if CPI spikes on oil but Core PCE stays tame, the Fed will look through it.",
  },

  // ── Labor Market ──────────────────────────────────────────────────────
  {
    key: "UnemploymentRate",
    name: "Unemployment",
    unit: "%",
    family: "Labor Market",
    short: "The share of the workforce without a job — the classic health check of the economy.",
    low: "Below ~4%: strong labor market and consumer spending — but also wage pressure that can keep inflation sticky.",
    high: "Above ~6%: economic weakness. Note: markets usually bottom long before unemployment peaks — it's a lagging indicator.",
    bands: "Healthy < 4% · Neutral 4-6% · Elevated > 6%",
    detail:
      "Unemployment is a lagging indicator with a momentum quirk: it moves slowly, then all at once. Small increases from a cycle low have historically snowballed (the insight behind the Sahm Rule). For markets, very low unemployment is double-edged — great for earnings, but it keeps the Fed hawkish.",
  },
  {
    key: "JoblessClaims",
    name: "Jobless Claims",
    unit: "",
    family: "Labor Market",
    short: "How many people filed for unemployment benefits this week — the fastest labor signal available.",
    low: "Under ~250k: companies are holding onto workers; the economy is strong.",
    high: "Over ~350k: layoffs are spreading — historically an early, fast confirmation that a downturn is underway.",
    bands: "Strong < 250k · Normal 250-350k · Elevated > 350k",
    detail:
      "Published weekly, claims lead the unemployment rate by months — they are the labor market's check-engine light. A sustained trend above 300-350k has accompanied every recession; short spikes from strikes or disasters are noise, the 4-week trend is the signal.",
  },
  {
    key: "SahmRule",
    name: "Sahm Rule",
    unit: "",
    family: "Labor Market",
    short: "A recession detector: triggers when unemployment rises 0.5pp above its 12-month low.",
    low: "Below 0.3: no recession signal from the labor market.",
    high: "At or above 0.5: the recession threshold — this signal has fired in every US recession since 1970 with essentially no false positives.",
    bands: "No Signal < 0.3 · Warning 0.3-0.5 · Recession Signal ≥ 0.5",
    detail:
      "Created by economist Claudia Sahm to trigger automatic stimulus, it captures the snowball dynamic of unemployment: once joblessness rises half a point from its low, it has historically kept rising substantially. It confirms recessions in real time rather than predicting them — pair it with the yield curve (which warns 12-18 months ahead) for the full sequence.",
  },

  // ── Growth & Consumer ─────────────────────────────────────────────────
  {
    key: "GDP",
    name: "GDP Growth",
    unit: "% YoY",
    family: "Growth & Consumer",
    short: "Total economic output versus a year ago — the broadest measure of growth.",
    low: "Negative: the economy is contracting — recession territory.",
    high: "Above ~2%: healthy expansion supporting corporate earnings.",
    bands: "Contracting < 0% · Slow 0-2% · Expanding > 2% YoY",
    detail:
      "GDP is comprehensive but slow — quarterly, revised repeatedly, and markets have usually priced in a slowdown long before GDP confirms it. Its value in regime matching is as context: the same Fed policy means something very different in a 3%-growth economy versus a stagnating one.",
  },
  {
    key: "RetailSales",
    name: "Retail Sales",
    unit: "% YoY",
    family: "Growth & Consumer",
    short: "How much consumers are spending in shops and online — consumption drives ~70% of the US economy.",
    low: "Falling sales mean consumers are pulling back — recessions are consumer events in the US.",
    high: "Strong growth above ~5% signals confident consumers and healthy corporate revenue.",
    bands: "Contracting < 0% · Normal 0-5% · Strong > 5% YoY",
    detail:
      "The US consumer is the engine of the global economy. Retail sales are nominal (not inflation-adjusted), so during high-inflation periods flat 'growth' actually means shrinking real consumption — read it together with CPI.",
  },
  {
    key: "HousingStarts",
    name: "Housing Starts",
    unit: "% YoY",
    family: "Growth & Consumer",
    short: "New home construction beginning each month — the most interest-rate-sensitive part of the economy.",
    low: "Falling more than ~10%: high rates are biting. Housing has led the economy into most recessions.",
    high: "Rising more than ~10%: cheap financing and confidence — an early-cycle expansion signal.",
    bands: "Falling < -10% · Stable -10% to +10% · Expanding > +10% YoY",
    detail:
      "Housing is where monetary policy hits the real economy first: mortgage rates follow the 10Y yield, and construction responds within months. 'Housing is the business cycle' is a famous economics paper title for a reason — it is one of the best leading indicators of both downturns and recoveries.",
  },
  {
    key: "ConsumerSentiment",
    name: "Consumer Sentiment",
    unit: "",
    family: "Growth & Consumer",
    short: "University of Michigan's survey of how ordinary Americans feel about the economy.",
    low: "Below 70: pessimism — though extreme lows have historically been contrarian buying opportunities for stocks.",
    high: "Above 90: optimism and willingness to spend — supportive, but euphoric readings can mark late-cycle complacency.",
    bands: "Pessimistic < 70 · Neutral 70-90 · Optimistic > 90",
    detail:
      "Sentiment matters because spending is partly psychological. The contrarian angle is real: the all-time sentiment low in mid-2022 marked, almost to the month, the bottom of that bear market. Extremes in either direction are more informative than the middle.",
  },

  // ── Market Stress & Sentiment ─────────────────────────────────────────
  {
    key: "VIX",
    name: "VIX",
    unit: "",
    family: "Market Stress & Sentiment",
    short: "The 'fear index' — how much volatility options traders expect in the S&P 500 over the next month.",
    low: "Below 15: calm, complacent markets. These regimes persist but leave markets exposed to shocks.",
    high: "Above 30: panic. Historically, forward 6-month equity returns after panic spikes are among the best of any regime.",
    bands: "Complacent < 15 · Neutral 15-20 · Elevated 20-30 · Panic > 30",
    detail:
      "The VIX is mean-reverting: it spends most of its life between 12 and 20 and spikes violently during crises (89 in 2008, 82 in 2020). That mean reversion is why extreme fear has been a contrarian opportunity — panic selling exhausts itself. A VIX that stays elevated for months, however, signals a genuine regime change rather than a passing scare.",
  },
  {
    key: "HighYieldSpread",
    name: "HY Credit Spread",
    unit: "%",
    family: "Market Stress & Sentiment",
    short: "The extra yield investors demand to hold junk bonds instead of Treasuries — the credit market's fear gauge.",
    low: "Below 4%: credit markets see low default risk. Major drawdowns rarely start when credit is calm.",
    high: "Above 6%: credit stress — historically one of the most reliable early warnings of recession. Credit leads equity.",
    bands: "Calm < 4% · Elevated 4-6% · Stressed > 6%",
    detail:
      "Bond investors are paid to be paranoid, which makes them better early-warning systems than stock investors. Spreads widening while stocks make new highs is a classic divergence that has preceded major tops. In 2008 spreads exceeded 20% — the scale of the signal matches the scale of the crisis.",
  },
  {
    key: "DollarIndex",
    name: "Dollar Index",
    unit: "",
    family: "Market Stress & Sentiment",
    short: "The US dollar's strength against major currencies — the world's funding currency.",
    low: "Below 95: a weak dollar is a tailwind for commodities, emerging markets, crypto, and US multinationals' earnings.",
    high: "Above 105: a strong dollar squeezes global liquidity — historically a headwind for nearly every risk asset.",
    bands: "Weak < 95 · Neutral 95-105 · Strong > 105",
    detail:
      "Most global debt and trade is priced in dollars, so a rising dollar effectively tightens conditions for the whole world. 'The dollar wrecking ball' is trader slang for what a sharp USD rally does to emerging markets and crypto. It usually strengthens when the Fed is hawkish or when global investors flee to safety — both risk-negative regimes.",
  },
  {
    key: "CryptoFearGreed",
    name: "Crypto Fear & Greed",
    unit: "/100",
    family: "Market Stress & Sentiment",
    short: "A 0-100 composite of crypto sentiment from volatility, momentum, social media, and dominance.",
    low: "Below 25 (Extreme Fear): capitulation territory — historically closer to bottoms than tops. A contrarian buy zone.",
    high: "Above 75 (Extreme Greed): euphoria — historically when tops form and disciplined traders take profit.",
    bands: "Extreme Fear < 25 · Fear 25-45 · Neutral 45-55 · Greed 55-75 · Extreme Greed > 75",
    detail:
      "Crypto is the most sentiment-driven major asset class, which makes sentiment extremes unusually informative. The index spent weeks below 10 at the 2018 and 2022 bear-market bottoms and pinned above 90 near the 2021 top. Like all contrarian gauges it is about zones, not precision timing — extreme fear can persist during a crash.",
  },

  // ── Commodities ───────────────────────────────────────────────────────
  {
    key: "GoldPrice",
    name: "Gold",
    unit: "% YoY",
    family: "Commodities",
    short: "Gold's price change versus a year ago — the classic fear and inflation hedge.",
    low: "Falling gold means low safe-haven demand — investors are comfortable taking risk.",
    high: "Gold up >15% YoY signals strong demand for safety: fear of inflation, war, or monetary debasement.",
    bands: "Risk-on < 0% · Neutral 0-15% · Risk-off > 15% YoY",
    detail:
      "Gold responds to two things: real yields (its opportunity cost) and fear (its safe-haven bid). A gold rally alongside rising stocks usually reflects falling real yields; a gold rally while stocks fall is genuine flight to safety. The distinction matters for reading the regime.",
  },
  {
    key: "OilWTI",
    name: "WTI Oil",
    unit: "% YoY",
    family: "Commodities",
    short: "US crude oil price change versus a year ago — the world's most important input cost.",
    low: "Oil down >20% signals weak global demand (bearish growth) but acts as a tax cut for consumers.",
    high: "Oil up >30% is an inflation shock — it feeds every price in the economy and pressures central banks to stay tight.",
    bands: "Deflationary < -20% · Neutral -20% to +30% · Inflationary > +30% YoY",
    detail:
      "Oil spikes preceded or accompanied the recessions of 1974, 1980, 1990, 2008, and the 2022 inflation crisis. The cause matters: demand-driven rallies (strong economy) are benign; supply-driven shocks (war, embargo) are stagflationary — rising costs with slowing growth, the worst combination for markets.",
  },
  {
    key: "Copper",
    name: "Copper",
    unit: "% YoY",
    family: "Commodities",
    short: "'Dr. Copper' — the metal in every building, car, and cable; a real-time global growth gauge.",
    low: "Down >15%: industrial demand is weakening — a bearish signal for global growth.",
    high: "Up >15%: factories and construction are humming — a bullish growth signal.",
    bands: "Contraction < -15% · Neutral -15% to +15% · Growth Signal > +15% YoY",
    detail:
      "Copper earned its 'PhD in economics' because demand comes almost entirely from real industrial activity. The copper/gold ratio is a bonus read: copper outperforming gold signals growth optimism; gold outperforming copper signals defensive positioning — it tracks the 10Y yield remarkably well.",
  },
];

export interface ComboInfo {
  title: string;
  indicators: string[]; // indicator keys involved
  text: string;
}

export const COMBOS: ComboInfo[] = [
  {
    title: "Fed Funds + CPI: is the Fed ahead or behind?",
    indicators: ["FedFundsRate", "CPI"],
    text:
      "Compare the policy rate to inflation. Fed rate above CPI = genuinely restrictive policy (the 'real rate' is positive) — inflation usually falls, and the next big move is typically easing. Fed rate below CPI = the Fed is 'behind the curve' (1970s, 2021) — expect more hikes than markets hope for. This single comparison explains most of the 2022 bear market: inflation at 9% with rates at 1% meant enormous tightening had to come.",
  },
  {
    title: "Yield Curve + Sahm Rule: the recession sequence",
    indicators: ["YieldCurveSpread", "SahmRule"],
    text:
      "These two form a timeline. The yield curve inverts first — a warning 12-18 months ahead. The Sahm Rule fires last — confirming the recession has effectively begun. The danger window is the transition: when the curve un-inverts (re-steepens) while the Sahm Rule climbs toward 0.5, history says the downturn is arriving, not ending. Inversion alone with a quiet Sahm Rule can persist for a year or more of decent returns.",
  },
  {
    title: "Fed Funds + Unemployment: the pivot setup",
    indicators: ["FedFundsRate", "UnemploymentRate"],
    text:
      "High rates + rising unemployment is the classic end-of-cycle combination: the Fed has cooled the economy and must now choose between inflation and jobs — it historically chooses jobs and cuts. Markets typically bottom during this window, before the cuts arrive. Conversely, low rates + low unemployment (an economy that doesn't need help getting it anyway) is the melt-up recipe — great returns, building excess.",
  },
  {
    title: "HY Spreads + VIX: is the fear real?",
    indicators: ["HighYieldSpread", "VIX"],
    text:
      "The VIX measures equity fear; credit spreads measure solvency fear. VIX spiking while spreads stay calm = an equity-market scare without economic damage — historically a buyable dip. Both spiking together = genuine credit stress, respect it. Spreads widening quietly while the VIX stays low is the most dangerous pattern: the bond market sees trouble the stock market is ignoring.",
  },
  {
    title: "Balance Sheet + Reverse Repo: the true liquidity picture",
    indicators: ["FedBalanceSheet", "ReverseRepo"],
    text:
      "Official QT (shrinking balance sheet) can be offset by cash draining out of the Reverse Repo facility back into markets — stealth easing. That combination explains 2023: the Fed tightened on paper while ~$1.5T of parked cash flowed back into assets. Real liquidity stress only arrives when the balance sheet shrinks AND the RRP buffer is empty.",
  },
  {
    title: "Gold + Real Yields: which gold rally is it?",
    indicators: ["GoldPrice", "RealYield10Y"],
    text:
      "Gold rising while real yields fall is mechanical — gold's opportunity cost is dropping. Normal. Gold rising while real yields also rise breaks the model — it signals investors are paying a premium for safety despite the cost: monetary distrust, geopolitical fear, or central-bank buying. That divergence (2023-24) is a regime signal worth respecting.",
  },
  {
    title: "Copper + Oil vs CPI: good inflation or bad inflation?",
    indicators: ["Copper", "OilWTI", "CPI"],
    text:
      "Copper and oil rising together with CPI = demand-driven inflation — a booming economy, historically fine for stocks in the early phase. Oil spiking while copper stagnates = supply-shock inflation — costs rising without growth. That's stagflation risk, the environment where both stocks and bonds fall together and only hard assets hold up (2022).",
  },
  {
    title: "Dollar + Fear & Greed: the crypto squeeze",
    indicators: ["DollarIndex", "CryptoFearGreed"],
    text:
      "Crypto's worst regime is a strong, rising dollar with extreme fear — global liquidity contracting while sentiment capitulates (2022). Its best is a weakening dollar with fear still low in the cycle — liquidity returning before the crowd notices. Extreme greed with a strengthening dollar is the take-profit warning: sentiment euphoric while the liquidity tide turns.",
  },
];

// The indicators most traders watch daily — highlighted in the Learn page TOC
export const MOST_WATCHED = new Set([
  "FedFundsRate",
  "CPI",
  "UnemploymentRate",
  "YieldCurveSpread",
  "TreasuryYield10Y",
  "VIX",
  "HighYieldSpread",
  "CryptoFearGreed",
]);

const byKey = new Map(INDICATORS.map((i) => [i.key, i]));

export function getIndicator(key: string): IndicatorInfo | undefined {
  return byKey.get(key);
}
