using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Api.Authentication;

public sealed class JwtTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);
    private readonly JwtSettings _settings;
    private readonly TimeProvider _clock;

    public JwtTokenService(JwtSettings settings, TimeProvider clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public (string Token, DateTimeOffset ExpiresAtUtc) Create(Usuario usuario)
    {
        var now = _clock.GetUtcNow();
        var expiresAt = now.Add(TokenLifetime);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_settings.SigningKey),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
                new Claim("role", usuario.Perfil.ToString())
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
