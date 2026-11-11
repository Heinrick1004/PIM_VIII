using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Contracts;
using TaskFlow.Web.Models;
using TaskFlow.Web.Services;

namespace TaskFlow.Web.Controllers;

[Authorize]
public sealed class HomeController(ITaskFlowApiClient apiClient) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var token = User.FindFirstValue("taskflow_access_token");
        if (token is null)
        {
            return await ExpireSessionAsync();
        }

        var pendingTask = apiClient.GetTasksAsync(
            token, 1, 1, StatusTarefa.Pendente, null, null, false, null, cancellationToken);
        var activeTask = apiClient.GetTasksAsync(
            token, 1, 1, StatusTarefa.EmAndamento, null, null, false, null, cancellationToken);
        var completedTask = apiClient.GetTasksAsync(
            token, 1, 1, StatusTarefa.Concluida, null, null, false, null, cancellationToken);
        var recentTask = apiClient.GetTasksAsync(
            token, 1, 5, null, null, null, false, null, cancellationToken);
        await Task.WhenAll(pendingTask, activeTask, completedTask, recentTask);

        var results = new[]
        {
            await pendingTask,
            await activeTask,
            await completedTask,
            await recentTask
        };
        if (results.Any(result => result.StatusCode == StatusCodes.Status401Unauthorized))
        {
            return await ExpireSessionAsync();
        }

        var failed = results.FirstOrDefault(result => !result.Succeeded || result.Value is null);
        if (failed is not null)
        {
            return View(new DashboardViewModel
            {
                Name = User.Identity?.Name ?? "",
                Profile = User.FindFirstValue(ClaimTypes.Role) ?? "",
                Error = failed.Message ?? "Não foi possível carregar o painel. Tente novamente."
            });
        }

        var pending = results[0].Value!;
        var active = results[1].Value!;
        var completed = results[2].Value!;
        var recent = results[3].Value!;
        return View(new DashboardViewModel
        {
            Name = User.Identity?.Name ?? "",
            Profile = User.FindFirstValue(ClaimTypes.Role) ?? "",
            Pending = pending.TotalCount,
            InProgress = active.TotalCount,
            Completed = completed.TotalCount,
            Total = pending.TotalCount + active.TotalCount + completed.TotalCount,
            RecentTasks = recent.Items
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Error() => View("~/Views/Account/Error.cshtml");

    private async Task<IActionResult> ExpireSessionAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Notice"] = "Sua sessão expirou. Entre novamente.";
        return RedirectToAction("Login", "Account");
    }
}
