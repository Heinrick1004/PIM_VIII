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

        var summaryTask = apiClient.GetTaskSummaryAsync(token, cancellationToken);
        var recentTask = apiClient.GetTasksAsync(token, 1, 5, null, null, null, false, null, cancellationToken);
        await Task.WhenAll(summaryTask, recentTask);
        var summary = await summaryTask;
        var recent = await recentTask;
        if (summary.StatusCode == StatusCodes.Status401Unauthorized || recent.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (!summary.Succeeded || summary.Value is null || !recent.Succeeded || recent.Value is null)
        {
            return View(new DashboardViewModel
            {
                Name = User.Identity?.Name ?? "",
                Profile = User.FindFirstValue(ClaimTypes.Role) ?? "",
                Error = summary.Message ?? recent.Message ?? "Não foi possível carregar o painel."
            });
        }
        return View(new DashboardViewModel
        {
            Name = User.Identity?.Name ?? "",
            Profile = User.FindFirstValue(ClaimTypes.Role) ?? "",
            Pending = summary.Value.Pending,
            InProgress = summary.Value.InProgress,
            Completed = summary.Value.Completed,
            Total = summary.Value.Total,
            RecentTasks = recent.Value.Items
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
