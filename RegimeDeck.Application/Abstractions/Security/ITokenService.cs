using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Application.Abstractions.Security;

public record AccessToken(string Token, DateTime Expiry);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
