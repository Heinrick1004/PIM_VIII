using System.Security.Cryptography;
using System.Text;

namespace TaskFlow.Api.Authentication;

public sealed class JwtSettings
{
    private JwtSettings(string issuer, string audience, byte[] signingKey)
    {
        Issuer = issuer;
        Audience = audience;
        SigningKey = signingKey;
    }

    public string Issuer { get; }
    public string Audience { get; }
    internal byte[] SigningKey { get; }

    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["Authentication:Jwt:Issuer"]?.Trim();
        var audience = configuration["Authentication:Jwt:Audience"]?.Trim();
        var encodedKey = configuration["Authentication:Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Configure o emissor e o público JWT.");
        }

        if (string.IsNullOrWhiteSpace(encodedKey))
        {
            throw new InvalidOperationException("Configure a chave de assinatura JWT externamente.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("A chave de assinatura JWT deve estar em Base64.", exception);
        }

        if (key.Length < 32)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException("A chave de assinatura JWT deve conter ao menos 256 bits.");
        }

        return new JwtSettings(issuer, audience, key);
    }
}
