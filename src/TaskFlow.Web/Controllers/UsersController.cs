using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Contracts;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

[Authorize(Roles = nameof(PerfilUsuario.Administrador))]
public sealed class UsersController(ITaskFlowApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000)
        {
            return BadRequest("A página é inválida.");
        }

        if (!TryGetToken(out var token))
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.GetUsersAsync(token, page, 10, cancellationToken);
        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        return View(new UserListViewModel
        {
            Results = result.Value,
            Page = page,
            PageSize = 10,
            Error = result.Succeeded ? null : result.Message
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new UserFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Informe uma senha com pelo menos 12 caracteres.");
        }

        if (!Enum.IsDefined(model.Profile))
        {
            ModelState.AddModelError(nameof(model.Profile), "Selecione um perfil válido.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        if (!TryGetToken(out var token))
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.CreateUserAsync(
            token,
            new CreateUserApiRequest(model.Name.Trim(), model.Email.Trim(), model.Password!, model.Profile),
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Não foi possível cadastrar o usuário.");
            return View("Form", model);
        }

        TempData["Success"] = "Usuário cadastrado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetToken(out var token))
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.GetUserAsync(token, id, cancellationToken);
        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (result.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        if (!result.Succeeded || result.Value is null)
        {
            return View("ApiError", result.Message ?? "Não foi possível carregar o usuário.");
        }

        return View("Form", new UserFormViewModel
        {
            Id = result.Value.Id,
            Name = result.Value.Nome,
            Email = result.Value.Email,
            Profile = result.Value.Perfil
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UserFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!Enum.IsDefined(model.Profile))
        {
            ModelState.AddModelError(nameof(model.Profile), "Selecione um perfil válido.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        if (!TryGetToken(out var token))
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.UpdateUserAsync(
            token,
            id,
            new UpdateUserApiRequest(model.Name.Trim(), model.Email.Trim(), model.Profile),
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Não foi possível atualizar o usuário.");
            return View("Form", model);
        }

        TempData["Success"] = "Usuário atualizado.";
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetToken(out string token)
    {
        token = User.FindFirstValue("taskflow_access_token") ?? "";
        return token.Length > 0;
    }

    private async Task<IActionResult> ExpireSessionAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Notice"] = "Sua sessão expirou. Entre novamente.";
        return RedirectToAction("Login", "Account");
    }
}
