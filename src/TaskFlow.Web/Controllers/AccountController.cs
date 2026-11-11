using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

public sealed class AccountController(
    ITaskFlowApiClient apiClient,
    TimeProvider clock) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await apiClient.LoginAsync(model.Email.Trim(), model.Password, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.StatusCode == StatusCodes.Status401Unauthorized
                    ? "E-mail ou senha incorretos."
                    : result.Message ?? "Não foi possível entrar. Tente novamente.");
            return View(model);
        }

        var authentication = result.Value;
        if (!string.Equals(authentication.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authentication.AccessToken)
            || authentication.ExpiresAtUtc <= clock.GetUtcNow())
        {
            ModelState.AddModelError(string.Empty, "A API retornou uma sessão inválida. Tente novamente.");
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, authentication.User.Id.ToString()),
            new Claim(ClaimTypes.Name, authentication.User.Nome),
            new Claim(ClaimTypes.Email, authentication.User.Email),
            new Claim(ClaimTypes.Role, authentication.User.Perfil.ToString()),
            new Claim("taskflow_access_token", authentication.AccessToken)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = authentication.ExpiresAtUtc
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Error() => View();
}
