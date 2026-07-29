using RegimeDeck.Contracts.Portfolio;

namespace RegimeDeck.Application.Portfolio;

public interface IPortfolioXrayService
{
    Task<PortfolioXrayDto> GetXrayAsync(PortfolioXrayRequest request, CancellationToken ct = default);
}
