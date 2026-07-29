using RegimeDeck.Contracts.Liquidity;

namespace RegimeDeck.Application.Liquidity;

public interface ILiquidityService
{
    // Fed net liquidity + global central-bank liquidity, their components,
    // the aligned weekly series and BTC/SPY co-movement.
    Task<LiquidityDashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
