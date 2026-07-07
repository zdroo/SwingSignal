using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using SwingSignal.Application.Abstractions.Security;

namespace SwingSignal.Infrastructure.Security;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string _clientId;

    public GoogleTokenValidator(IConfiguration config)
    {
        _clientId = config["Google:ClientId"]
            ?? throw new InvalidOperationException("Google:ClientId not configured");
    }

    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_clientId]
                });

            return new GoogleUserInfo(payload.Email, payload.Name);
        }
        catch (InvalidJwtException ex)
        {
            throw new UnauthorizedAccessException($"Invalid Google token: {ex.Message}");
        }
    }
}
