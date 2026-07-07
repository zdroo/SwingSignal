// Shared price formatting — always shows the currency the asset is quoted in.
// Forex pairs are quoted in their SECOND currency (USD/JPY prices are in yen).

export function quoteCurrency(symbol: string): string {
  const s = symbol.toUpperCase();

  // Yahoo forex format: EURUSD=X -> quote currency is chars 3-6
  if (s.endsWith("=X") && s.length >= 8) return s.slice(3, 6);

  // Binance stablecoin pairs are dollar-equivalent
  if (s.endsWith("USDT") || s.endsWith("BUSD")) return "USD";

  // US futures, stocks, ETFs, indices (our Yahoo universe) quote in USD
  return "USD";
}

export function formatPrice(price: number, symbol: string): string {
  const currency = quoteCurrency(symbol);

  const digits = price > 1000 ? 0 : price > 1 ? 2 : 4;

  try {
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: digits,
    }).format(price);
  } catch {
    // Unknown currency code — fall back to plain number + code
    return `${price.toLocaleString("en-US", { maximumFractionDigits: digits })} ${currency}`;
  }
}
