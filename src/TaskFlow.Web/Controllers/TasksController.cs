using System.Globalization;
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
public sealed class TasksController(
    ITaskFlowApiClient apiClient,
    TaskFlowUserDirectory userDirectory) : Controller
{
    private string? Token => User.FindFirstValue("taskflow_access_token");
    private bool IsAdministrator => User.IsInRole(nameof(PerfilUsuario.Administrador));
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 10,
        StatusTarefa? status = null,
        PrioridadeTarefa? prioridade = null,
        Guid? responsavelId = null,
        bool semResponsavel = false,
        string? titulo = null,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
        {
            return BadRequest("Os parâmetros de paginação são inválidos.");
        }

        if (Token is not { } token)
        {
            return await ExpireSessionAsync();
        }

        var canUseAssignmentFilters = IsAdministrator;
        var result = await apiClient.GetTasksAsync(
            token,
            page,
            pageSize,
            status,
            prioridade,
            canUseAssignmentFilters ? responsavelId : null,
            canUseAssignmentFilters && semResponsavel,
            titulo,
            cancellationToken);
        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (result.StatusCode == StatusCodes.Status403Forbidden)
        {
            return Forbid();
        }

        IReadOnlyDictionary<Guid, string> names = new Dictionary<Guid, string>();
        string? directoryError = null;
        if (IsAdministrator && result.Value is not null)
        {
            var userIds = result.Value.Items
                .Where(task => task.ResponsavelId.HasValue)
                .Select(task => task.ResponsavelId!.Value);
            var resolved = await userDirectory.ResolveNamesAsync(token, userIds, cancellationToken);
            if (resolved.Succeeded && resolved.Value is not null)
            {
                names = resolved.Value;
            }
            else if (resolved.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return await ExpireSessionAsync();
            }
            else
            {
                directoryError = "Os nomes dos responsáveis não puderam ser carregados.";
            }
        }

        return View(new TaskListViewModel
        {
            Results = result.Value,
            UserNames = names,
            Page = page,
            PageSize = pageSize,
            Status = status,
            Priority = prioridade,
            ResponsibleId = canUseAssignmentFilters ? responsavelId : null,
            WithoutResponsible = canUseAssignmentFilters && semResponsavel,
            Title = titulo,
            IsAdministrator = IsAdministrator,
            Error = result.Succeeded ? directoryError : result.Message
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000)
        {
            return BadRequest("A página do histórico é inválida.");
        }

        if (Token is not { } token)
        {
            return await ExpireSessionAsync();
        }

        var taskResult = await apiClient.GetTaskAsync(token, id, cancellationToken);
        if (taskResult.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (taskResult.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        if (taskResult.StatusCode == StatusCodes.Status403Forbidden)
        {
            return Forbid();
        }

        if (!taskResult.Succeeded || taskResult.Value is null)
        {
            return View("ApiError", taskResult.Message ?? "Não foi possível carregar a tarefa.");
        }

        var historyResult = await apiClient.GetTaskHistoryAsync(token, id, page, 10, cancellationToken);
        if (historyResult.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return await ExpireSessionAsync();
        }

        if (historyResult.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        if (historyResult.StatusCode == StatusCodes.Status403Forbidden)
        {
            return Forbid();
        }

        var task = taskResult.Value;
        var userNames = new Dictionary<Guid, string>();
        if (IsAdministrator)
        {
            var userIds = new[] { task.CriadorId }
                .Concat(task.ResponsavelId.HasValue ? [task.ResponsavelId.Value] : [])
                .Concat(historyResult.Value?.Items.Select(item => item.UsuarioId) ?? [])
                .Distinct();
            var resolved = await userDirectory.ResolveNamesAsync(token, userIds, cancellationToken);
            if (resolved.Succeeded && resolved.Value is not null)
            {
                userNames = new Dictionary<Guid, string>(resolved.Value);
            }
            else if (resolved.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return await ExpireSessionAsync();
            }
        }

        return View(new TaskDetailsViewModel
        {
            Task = task,
            History = historyResult.Value,
            UserNames = userNames,
            HistoryPage = page,
            IsAdministrator = IsAdministrator,
            CanChangeStatus = IsAdministrator || task.ResponsavelId == UserId,
            Error = historyResult.Succeeded ? null : historyResult.Message
        });
    }

    [HttpGet]
    [Authorize(Roles = nameof(PerfilUsuario.Administrador))]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = new TaskFormViewModel();
        if (!await LoadEmployeesAsync(model, cancellationToken))
        {
            return View("Form", model);
        }

        return View("Form", model);
    }

    [HttpPost]
    [Authorize(Roles = nameof(PerfilUsuario.Administrador))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskFormViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetDeadline(model, out var deadline))
        {
            ModelState.AddModelError(nameof(model.DueLocal), "Confira o prazo local e o fuso horário do navegador.");
        }

        if (!Enum.IsDefined(model.Priority))
        {
            ModelState.AddModelError(nameof(model.Priority), "Selecione uma prioridade válida.");
        }

        if (!ModelState.IsValid)
        {
            await LoadEmployeesAsync(model, cancellationToken);
            return View("Form", model);
        }

        if (Token is not { } token)
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.CreateTaskAsync(
            token,
            new CreateTaskApiRequest(
                model.Title,
                Normalize(model.Description),
                model.Priority,
                deadline,
                model.ResponsibleId),
            cancellationToken);
        if (await ExpireForUnauthorizedAsync(result.StatusCode))
        {
            return RedirectToAction("Login", "Account");
        }

        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Não foi possível criar a tarefa.");
            await LoadEmployeesAsync(model, cancellationToken);
            return View("Form", model);
        }

        TempData["Success"] = "Tarefa criada.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    [Authorize(Roles = nameof(PerfilUsuario.Administrador))]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (Token is not { } token)
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.GetTaskAsync(token, id, cancellationToken);
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
            return View("ApiError", result.Message ?? "Não foi possível carregar a tarefa.");
        }

        var task = result.Value;
        var model = new TaskFormViewModel
        {
            Id = task.Id,
            Title = task.Titulo,
            Description = task.Descricao,
            Priority = task.Prioridade,
            DueUtcInitial = task.PrazoUtc?.ToString("O", CultureInfo.InvariantCulture),
            ResponsibleId = task.ResponsavelId,
            RowVersion = task.RowVersion
        };
        await LoadEmployeesAsync(model, cancellationToken);
        return View("Form", model);
    }

    [HttpPost]
    [Authorize(Roles = nameof(PerfilUsuario.Administrador))]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, TaskFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (string.IsNullOrWhiteSpace(model.RowVersion))
        {
            ModelState.AddModelError(nameof(model.RowVersion), "Atualize a tarefa antes de salvar.");
        }

        if (!TryGetDeadline(model, out var deadline))
        {
            ModelState.AddModelError(nameof(model.DueLocal), "Confira o prazo local e o fuso horário do navegador.");
        }

        if (!Enum.IsDefined(model.Priority))
        {
            ModelState.AddModelError(nameof(model.Priority), "Selecione uma prioridade válida.");
        }

        if (!ModelState.IsValid)
        {
            await LoadEmployeesAsync(model, cancellationToken);
            return View("Form", model);
        }

        if (Token is not { } token)
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.UpdateTaskAsync(
            token,
            id,
            new UpdateTaskApiRequest(
                model.Title,
                Normalize(model.Description),
                model.Priority,
                deadline,
                model.ResponsibleId,
                model.RowVersion!),
            cancellationToken);
        if (await ExpireForUnauthorizedAsync(result.StatusCode))
        {
            return RedirectToAction("Login", "Account");
        }

        if (result.StatusCode == StatusCodes.Status409Conflict)
        {
            TempData["Error"] = "A tarefa foi alterada por outra pessoa. Recarregue o formulário antes de tentar novamente.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Não foi possível salvar as alterações.");
            await LoadEmployeesAsync(model, cancellationToken);
            return View("Form", model);
        }

        TempData["Success"] = "Tarefa atualizada.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        StatusTarefa status,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(status))
        {
            return BadRequest("O status informado é inválido.");
        }

        if (Token is not { } token)
        {
            return await ExpireSessionAsync();
        }

        var result = await apiClient.UpdateTaskStatusAsync(
            token,
            id,
            new UpdateTaskStatusApiRequest(status, rowVersion),
            cancellationToken);
        if (await ExpireForUnauthorizedAsync(result.StatusCode))
        {
            return RedirectToAction("Login", "Account");
        }

        if (result.StatusCode == StatusCodes.Status409Conflict)
        {
            TempData["Error"] = "A tarefa mudou desde que você a abriu. O status não foi alterado; confira os dados atualizados.";
        }
        else if (!result.Succeeded)
        {
            TempData["Error"] = result.Message ?? "Não foi possível alterar o status.";
        }
        else
        {
            TempData["Success"] = "Status atualizado.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<bool> LoadEmployeesAsync(TaskFormViewModel model, CancellationToken cancellationToken)
    {
        if (Token is not { } token)
        {
            await ExpireSessionAsync();
            return false;
        }

        var firstPage = await apiClient.GetUsersAsync(token, 1, 100, cancellationToken);
        if (firstPage.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await ExpireSessionAsync();
            return false;
        }

        if (!firstPage.Succeeded || firstPage.Value is null)
        {
            ModelState.AddModelError(string.Empty, firstPage.Message ?? "Não foi possível carregar os responsáveis.");
            return false;
        }

        var users = firstPage.Value.Items.ToList();
        var totalPages = (int)Math.Ceiling(firstPage.Value.TotalCount / 100d);
        if (totalPages > 10_000)
        {
            ModelState.AddModelError(string.Empty, "A lista de usuários excede o limite suportado pelo formulário.");
            return false;
        }

        for (var page = 2; page <= totalPages; page++)
        {
            var result = await apiClient.GetUsersAsync(token, page, 100, cancellationToken);
            if (result.StatusCode == StatusCodes.Status401Unauthorized)
            {
                await ExpireSessionAsync();
                return false;
            }

            if (!result.Succeeded || result.Value is null)
            {
                ModelState.AddModelError(string.Empty, result.Message ?? "Não foi possível carregar os responsáveis.");
                return false;
            }

            users.AddRange(result.Value.Items);
        }

        model.Employees = users
            .Where(user => user.Perfil == PerfilUsuario.Funcionario)
            .Select(user => new UserOption(user.Id, $"{user.Nome} · {user.Email}"))
            .ToArray();
        return true;
    }

    private static bool TryGetDeadline(TaskFormViewModel model, out DateTime? deadline)
    {
        deadline = null;
        if (string.IsNullOrWhiteSpace(model.DueLocal))
        {
            return true;
        }

        if (model.TimezoneOffsetMinutes is not (>= -840 and <= 840)
            || !DateTime.TryParseExact(
                model.DueLocal,
                ["yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localTime))
        {
            return false;
        }

        deadline = DateTime.SpecifyKind(localTime.AddMinutes(model.TimezoneOffsetMinutes.Value), DateTimeKind.Utc);
        return true;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<bool> ExpireForUnauthorizedAsync(int statusCode)
    {
        if (statusCode != StatusCodes.Status401Unauthorized)
        {
            return false;
        }

        await ExpireSessionAsync();
        return true;
    }

    private async Task<IActionResult> ExpireSessionAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Notice"] = "Sua sessão expirou. Entre novamente.";
        return RedirectToAction("Login", "Account");
    }
}
