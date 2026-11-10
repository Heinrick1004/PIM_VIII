namespace TaskFlow.Tests;

internal static class TestJwtConfiguration
{
    public const string Issuer = "TaskFlow.Tests";
    public const string Audience = "TaskFlow.Tests.Api";
    public static string SigningKey { get; } =
        Convert.ToBase64String(Enumerable.Range(0, 64).Select(value => (byte)value).ToArray());
}
