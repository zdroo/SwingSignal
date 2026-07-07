// Financial dictionary written for people new to investing (think 16-17 years old).
// Plain language, everyday analogies, zero jargon-explaining-jargon.

export interface GlossaryTerm {
  term: string;
  theme: string;
  definition: string;
  analogy?: string;
}

export const GLOSSARY_THEMES = [
  "Money Basics",
  "The Economy",
  "Stocks & Markets",
  "Bonds & Interest Rates",
  "Trading Terms",
  "Crypto",
] as const;

export const GLOSSARY: GlossaryTerm[] = [
  // ── Money Basics ──────────────────────────────────────────────────────
  {
    term: "Inflation",
    theme: "Money Basics",
    definition:
      "Prices going up over time, which means your money buys less than it used to. If inflation is 5%, something that cost $100 last year costs $105 now — your money silently lost 5% of its power.",
    analogy:
      "Your favorite menu item cost $8 two years ago and $10 today. That's inflation eating your allowance.",
  },
  {
    term: "Interest Rate",
    theme: "Money Basics",
    definition:
      "The price of borrowing money, shown as a percentage. Borrow $100 at 10% interest and you'll pay back $110. It also works in reverse: it's what the bank pays YOU for keeping money there.",
    analogy:
      "Lend your friend $20 and ask for $22 back next month — you just charged 10% interest.",
  },
  {
    term: "Liquidity",
    theme: "Money Basics",
    definition:
      "How easily something can be turned into cash without losing value. Money in your bank account is fully liquid; a house is not — selling it takes months. Markets with lots of liquidity are easy to trade in; when liquidity dries up, prices move violently.",
    analogy:
      "Selling a popular gaming console is easy — everyone wants one (liquid). Selling your grandma's porcelain collection takes months to find a buyer (illiquid).",
  },
  {
    term: "Asset",
    theme: "Money Basics",
    definition:
      "Anything you own that has value and could make you money: stocks, crypto, a house, gold. Investors talk about 'risk assets' (stocks, crypto — can grow a lot, can fall a lot) versus 'safe assets' (government bonds, cash — boring but stable).",
  },
  {
    term: "Portfolio",
    theme: "Money Basics",
    definition:
      "Everything you've invested in, viewed as one collection. If you own two stocks, some Bitcoin, and a savings account, that's your portfolio.",
    analogy: "Your portfolio is your team roster — each investment is a player with a different role.",
  },
  {
    term: "Diversification",
    theme: "Money Basics",
    definition:
      "Not putting all your money in one thing. If you own 10 different investments and one crashes, you lose a little. If you own only that one, you lose everything.",
    analogy: "Don't carry all your eggs in one basket — drop the basket, lose every egg.",
  },
  {
    term: "Compound Growth",
    theme: "Money Basics",
    definition:
      "Earning returns on your returns. Year one, $100 grows 10% to $110. Year two, you earn 10% on $110, not $100. Over decades this snowballs — it's the single biggest advantage of starting young.",
    analogy:
      "A snowball rolling downhill: it picks up snow, gets bigger, and then picks up even more snow because it's bigger.",
  },

  // ── The Economy ───────────────────────────────────────────────────────
  {
    term: "The Fed (Federal Reserve)",
    theme: "The Economy",
    definition:
      "America's central bank — the referee of the entire economy. It sets the base interest rate that everything else follows. When the economy overheats (inflation), the Fed raises rates to cool it down. When the economy struggles, it cuts rates to help.",
    analogy:
      "The Fed is like a thermostat for the economy: too hot (inflation) → turn rates up to cool it; too cold (recession) → turn rates down to warm it.",
  },
  {
    term: "Recession",
    theme: "The Economy",
    definition:
      "When the economy shrinks instead of growing: companies sell less, people lose jobs, everyone spends less, which makes companies sell even less. Usually lasts months to a couple of years. Stock markets typically fall before a recession is official and recover before it ends.",
  },
  {
    term: "GDP",
    theme: "The Economy",
    definition:
      "Gross Domestic Product — the total value of everything a country produces in a year. It's the economy's score. Growing GDP = healthy economy; shrinking GDP = recession territory.",
  },
  {
    term: "Central Bank Printing (QE)",
    theme: "The Economy",
    definition:
      "Quantitative Easing — when the central bank creates new money to buy bonds, pushing cash into the financial system. More money chasing the same assets usually pushes prices up. The reverse (QT, quantitative tightening) drains money out.",
    analogy:
      "Imagine the school suddenly doubles everyone's lunch money. The cafeteria doesn't have more food, so snack prices rise. That's QE for asset prices.",
  },
  {
    term: "Bull Market / Bear Market",
    theme: "The Economy",
    definition:
      "A bull market is a long period of rising prices and optimism. A bear market is a fall of 20% or more with widespread pessimism. Names come from how the animals attack: a bull thrusts its horns up, a bear swipes down.",
  },
  {
    term: "Market Sentiment",
    theme: "The Economy",
    definition:
      "The overall mood of investors — greedy or fearful. Extreme moods are often wrong at the extremes: when everyone is euphoric there's no one left to buy, and when everyone has panicked there's no one left to sell. That's why 'fear indexes' exist.",
  },

  // ── Stocks & Markets ──────────────────────────────────────────────────
  {
    term: "Stock (Share)",
    theme: "Stocks & Markets",
    definition:
      "A tiny piece of ownership in a company. Own Apple stock and you literally own a slice of Apple — if the company grows more valuable, your slice does too.",
    analogy:
      "A company is a pizza cut into millions of slices. Buying a stock buys you a slice — if the whole pizza becomes more desirable, your slice is worth more.",
  },
  {
    term: "Index (S&P 500, NASDAQ)",
    theme: "Stocks & Markets",
    definition:
      "A basket of many stocks measured together as one number. The S&P 500 tracks the 500 biggest US companies — when people say 'the market is up', they usually mean this index. The NASDAQ 100 leans toward tech companies.",
    analogy:
      "Instead of checking every student's grade, you look at the class average. An index is the market's class average.",
  },
  {
    term: "ETF",
    theme: "Stocks & Markets",
    definition:
      "Exchange-Traded Fund — a single thing you can buy that contains many investments inside. Buying one share of SPY gets you a tiny piece of all 500 companies in the S&P 500 at once. The easiest way to diversify with little money.",
    analogy: "A playlist instead of a single song — one click, you get the whole collection.",
  },
  {
    term: "Dividend",
    theme: "Stocks & Markets",
    definition:
      "Cash a company pays its shareholders from its profits, usually every few months. Not all companies pay them — younger companies typically reinvest everything into growth instead.",
    analogy: "Like your slice of the pizza shop's profits landing in your pocket every quarter.",
  },
  {
    term: "Market Cap",
    theme: "Stocks & Markets",
    definition:
      "What the entire company is worth on the market: share price × number of shares. A $2 stock isn't 'cheaper' than a $200 stock — what matters is the whole company's value.",
  },
  {
    term: "Volatility",
    theme: "Stocks & Markets",
    definition:
      "How wildly the price swings. High volatility means big moves in both directions — bigger potential gains AND bigger potential losses. Crypto is very volatile; government bonds are calm. The VIX index measures expected volatility for stocks.",
    analogy: "A calm lake vs. ocean waves in a storm. Same water, very different ride.",
  },
  {
    term: "Correlation",
    theme: "Stocks & Markets",
    definition:
      "How much two things move together. If Bitcoin and tech stocks rise and fall on the same days, they're highly correlated — owning both doesn't diversify you as much as you'd think.",
    analogy: "Two friends who always show up together. Invite one, you've effectively invited both.",
  },

  // ── Bonds & Interest Rates ────────────────────────────────────────────
  {
    term: "Bond",
    theme: "Bonds & Interest Rates",
    definition:
      "An IOU you can buy. You lend money to a government or company; they promise to pay you interest and return the full amount on a set date. Safer than stocks, but with less upside. US government bonds (Treasuries) are considered the safest investment in the world.",
    analogy:
      "You lend a reliable friend $100; they sign a note promising $4 a year and the $100 back in ten years. You just bought a bond.",
  },
  {
    term: "Yield",
    theme: "Bonds & Interest Rates",
    definition:
      "The yearly return an investment pays you, as a percentage of its price. A bond paying $4 a year that costs $100 yields 4%. Key twist: when a bond's price falls, its yield rises — the payments are fixed, so a cheaper price means a better deal for new buyers.",
    analogy:
      "A $10/month streaming subscription 'yields' more entertainment per euro if you catch it on sale for $5 — same content, better return on what you paid.",
  },
  {
    term: "Treasury",
    theme: "Bonds & Interest Rates",
    definition:
      "A bond issued by the US government. The '10-year Treasury yield' — what the US pays to borrow for 10 years — is the most important number in global finance: mortgages, company loans, and stock valuations all key off it.",
  },
  {
    term: "Yield Curve",
    theme: "Bonds & Interest Rates",
    definition:
      "A comparison of short-term vs long-term interest rates. Normally, lending for 10 years pays more than lending for 3 months (you're locked in longer, you deserve more). When short-term pays MORE than long-term, the curve is 'inverted' — historically the most famous recession warning in finance.",
    analogy:
      "Normally a 10-hour babysitting job pays more than a 1-hour one. If someone suddenly pays more for the 1-hour job, something strange is going on — that's an inverted curve.",
  },
  {
    term: "Credit Spread",
    theme: "Bonds & Interest Rates",
    definition:
      "The extra interest risky companies must pay to borrow compared to the ultra-safe US government. When investors get scared, they demand much more to lend to risky companies — widening spreads are one of the earliest, most reliable warnings of economic trouble.",
    analogy:
      "You'd lend your responsible cousin money at 5%, but you'd want 15% from the friend who never pays anyone back. That 10% gap is the credit spread — and it grows when you get worried about him.",
  },

  // ── Trading Terms ─────────────────────────────────────────────────────
  {
    term: "Odds / Probability",
    theme: "Trading Terms",
    definition:
      "The chance of something happening, out of 100. A '70% chance SPY rises in 3 months' means: in situations like this one, history says it rose about 7 times out of 10 — and fell 3 times out of 10. Odds are never a promise; 70% things fail all the time.",
    analogy:
      "A weather forecast. 70% chance of sun doesn't mean no umbrella exists — it rains 3 days out of 10 with that exact forecast.",
  },
  {
    term: "Base Rate",
    theme: "Trading Terms",
    definition:
      "How often something normally happens, before any special analysis. SPY has risen in roughly 70% of ALL 3-month periods in history — that's its base rate. A prediction is only impressive if it beats the base rate. SwingSignal always shows you both, so you can see what the analysis actually adds.",
    analogy:
      "Your team wins 70% of games in general. Predicting a win for tomorrow isn't a hot take — it's the base rate. Claiming 90% is the actual bet.",
  },
  {
    term: "Percentage Point (pp)",
    theme: "Trading Terms",
    definition:
      "The unit for the DIFFERENCE between two percentages. If odds go from 70% to 72%, they rose by 2 percentage points (2pp) — not by 2%. Saying '2%' would technically mean 2% OF 70, which is just 1.4. Finance people use 'pp' to avoid exactly that confusion. When SwingSignal shows an edge like '+2pp', it means the current situation adds 2 percentage points on top of the normal odds.",
    analogy:
      "Your test average goes from 80 to 85. That's +5 percentage points. '5% better than 80' would only be 84 — small difference here, big difference when money is involved.",
  },
  {
    term: "Edge",
    theme: "Trading Terms",
    definition:
      "How much better (or worse) your odds are compared to normal. If an asset rises in 70% of all 3-month periods (its base rate), and in conditions like today it rose 74% of the time, the edge is +4pp. Edge is the honest measure of whether an analysis actually adds anything — most of the time, for most assets, the edge is small. Anyone claiming huge edges everywhere is selling something.",
    analogy:
      "A basketball player hits 75% of free throws normally, but 79% at home games. Playing at home is a +4pp edge — real, but it doesn't turn a miss into a guarantee.",
  },
  {
    term: "Drawdown",
    theme: "Trading Terms",
    definition:
      "How far an investment has fallen from its highest point. If your $1,000 grew to $1,500 then dropped to $1,200, you're in a 20% drawdown. Every investor experiences them — the question is whether you sized your bets so you can survive them.",
  },
  {
    term: "Leverage",
    theme: "Trading Terms",
    definition:
      "Investing with borrowed money to multiply gains — and losses. With 10x leverage, a 5% gain becomes 50%, but a 10% drop wipes out everything you put in ('liquidation'). It's the #1 way beginners lose all their money fast, especially in crypto.",
    analogy:
      "Riding a bike downhill is fun. Leverage is riding it downhill at 10x speed with no brakes — thrilling until the smallest pebble.",
  },
  {
    term: "Hedge",
    theme: "Trading Terms",
    definition:
      "An investment made to protect other investments — something likely to go UP if your main holdings go DOWN. Gold is a classic hedge against market chaos.",
    analogy: "Buying insurance for your bike. Costs a little, pays off exactly when things go wrong.",
  },
  {
    term: "Short Selling",
    theme: "Trading Terms",
    definition:
      "Betting that a price will FALL. You borrow shares, sell them now, and hope to buy them back cheaper later. Dangerous for beginners: a normal bet can lose 100% at most; a short can lose infinitely, because there's no ceiling on how high a price can go.",
  },
  {
    term: "Backtest",
    theme: "Trading Terms",
    definition:
      "Testing a strategy against historical data: 'if I had followed this rule for the last 30 years, what would have happened?' Useful but tricky — it's easy to build something that explains the past perfectly and fails in the future. That's why honest backtests test on data the strategy has never seen.",
    analogy:
      "Practicing on last year's exam papers. Helpful — but acing old exams doesn't guarantee acing the new one, especially if you memorized the answers instead of learning the method.",
  },

  // ── Crypto ────────────────────────────────────────────────────────────
  {
    term: "Bitcoin Halving",
    theme: "Crypto",
    definition:
      "Every ~4 years, the reward for mining new Bitcoin is cut in half, slowing how fast new coins are created. Less new supply with the same demand has historically preceded Bitcoin's biggest bull runs — though with only a few halvings ever, the pattern is far from guaranteed.",
    analogy:
      "Imagine the sneaker company halving production of a hyped model every 4 years. Same fans, fewer new pairs → resale prices historically jumped.",
  },
  {
    term: "Altcoin",
    theme: "Crypto",
    definition:
      "Any cryptocurrency that isn't Bitcoin. Thousands exist — a few are serious projects, most are not. Altcoins usually amplify Bitcoin's moves: they rise harder in good times and crash harder in bad times.",
  },
  {
    term: "Stablecoin",
    theme: "Crypto",
    definition:
      "A cryptocurrency designed to always be worth exactly $1 (like USDT or USDC). Traders use them as a parking spot between trades. Total stablecoin supply is a useful signal: growing supply means money is entering crypto, waiting to buy something.",
    analogy: "Casino chips. Not the game itself — but the amount of chips at the tables tells you how busy the casino is.",
  },
  {
    term: "BTC Dominance",
    theme: "Crypto",
    definition:
      "Bitcoin's share of the entire crypto market's value. Rising dominance = money hiding in the 'safest' crypto (caution). Falling dominance = money chasing riskier altcoins (greed phase, aka 'altseason').",
  },
  {
    term: "Funding Rate",
    theme: "Crypto",
    definition:
      "A small payment between traders betting up vs down in crypto futures markets. When it's very positive, too many people are betting up with leverage — historically a warning that the market is overheated and due for a shakeout.",
  },
];
