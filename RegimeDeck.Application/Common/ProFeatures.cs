namespace RegimeDeck.Application.Common;

/// Dark-launch switch for the Pro tier. Entitlement (the user's plan) is
/// always enforced where a gate exists, but no gate bites while the flag is
/// off — everything currently free stays free until Pro actually launches.
/// Flip Features:ProEnabled in config to turn the paid boundary on.
public sealed class ProFeatures
{
    /// Custom prediction windows per day for Free accounts once Pro is live.
    /// Generous for a human checking setups; a wall for systematic scraping.
    public const int CustomWindowDailyLimit = 20;

    public bool Enabled { get; }

    public ProFeatures(bool enabled) => Enabled = enabled;

    /// True when this gate should actually restrict the caller.
    public bool GateActive(bool isPro) => Enabled && !isPro;

    public void RequirePro(bool isPro, string message)
    {
        if (GateActive(isPro))
            throw new ForbiddenException(message);
    }
}
