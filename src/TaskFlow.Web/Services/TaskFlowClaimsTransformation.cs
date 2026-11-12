using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TaskFlow.Web.Services;

public sealed class TaskFlowClaimsTransformation(
    ITaskFlowApiClient apiClient,
    IHttpContextAccessor httpContextAccessor) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        var token = principal.FindFirst("taskflow_access_token")?.Value;
        if (string.IsNullOrWhiteSpace(token))
        {
            return UnauthenticatedPrincipal();
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var result = await apiClient.GetCurrentUserAsync(token, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            if (result.StatusCode == StatusCodes.Status401Unauthorized
                && httpContextAccessor.HttpContext is { } context)
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return UnauthenticatedPrincipal();
        }

        var user = result.Value;
        var updatedClaims = identity.Claims
            .Where(claim => claim.Type is not ClaimTypes.Name
                and not ClaimTypes.Email
                and not ClaimTypes.Role
                and not ClaimTypes.NameIdentifier
                and not "taskflow_access_token")
            .Select(claim => new Claim(claim.Type, claim.Value))
            .Append(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()))
            .Append(new Claim(ClaimTypes.Name, user.Nome))
            .Append(new Claim(ClaimTypes.Email, user.Email))
            .Append(new Claim(ClaimTypes.Role, user.Perfil.ToString()))
            .Append(new Claim("taskflow_access_token", token))
            .ToList();

        var refreshedIdentity = new ClaimsIdentity(
            updatedClaims,
            identity.AuthenticationType,
            ClaimTypes.Name,
            ClaimTypes.Role);

        return new ClaimsPrincipal(refreshedIdentity);
    }

    private static ClaimsPrincipal UnauthenticatedPrincipal() =>
        new(new ClaimsIdentity());
}
