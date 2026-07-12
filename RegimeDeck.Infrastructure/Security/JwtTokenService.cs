using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RegimeDeck.Application.Abstractions.Security;
using RegimeDeck.Domain.Entities;

namespace RegimeDeck.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromDays(7);

    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public AccessToken CreateAccessToken(User user)
    {
        var expiry = DateTime.UtcNow.Add(AccessTokenLifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("plan", user.Plan.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret not configured")));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiry);
    }
}
