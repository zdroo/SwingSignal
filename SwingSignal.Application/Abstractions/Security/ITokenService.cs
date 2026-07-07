using SwingSignal.Domain.Entities;

namespace SwingSignal.Application.Abstractions.Security;

public record AccessToken(string Token, DateTime Expiry);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
