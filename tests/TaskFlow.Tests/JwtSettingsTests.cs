using Microsoft.Extensions.Configuration;
using TaskFlow.Api.Authentication;

namespace TaskFlow.Tests;

[Trait("Category", "Security")]
public sealed class JwtSettingsTests
{
    [Fact]
    public void MissingSigningKey_FailsExplicitlyInsteadOfUsingADevelopmentFallback()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "TaskFlow.Tests",
                ["Authentication:Jwt:Audience"] = "TaskFlow.Tests.Api"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() => JwtSettings.FromConfiguration(configuration));
        Assert.Contains("externamente", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SigningKeyShorterThan256Bits_IsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Jwt:Issuer"] = "TaskFlow.Tests",
                ["Authentication:Jwt:Audience"] = "TaskFlow.Tests.Api",
                ["Authentication:Jwt:SigningKey"] = Convert.ToBase64String(new byte[31])
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => JwtSettings.FromConfiguration(configuration));
    }
}
