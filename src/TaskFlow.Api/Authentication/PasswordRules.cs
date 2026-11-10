namespace TaskFlow.Api.Authentication;

public static class PasswordRules
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password)
        && password.Length >= MinimumLength
        && password.Length <= MaximumLength;
}
