namespace RegimeDeck.Contracts.Billing;

public record CheckoutRequest(string Period); // "monthly" | "yearly"

public record BillingUrlDto(string Url);
