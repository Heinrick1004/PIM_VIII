using System.Security.Claims;
using TaskFlow.Api.Contracts;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Endpoints;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tasks")
            .WithTags("Tarefas")
            .RequireAuthorization();

        group.MapPost("/", CreateAsync)
            .RequireAuthorization("Administrator");
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/history", GetHistoryAsync);
        return group;
    }

    private static async Task<IResult> CreateAsync(
        CreateTaskRequest? request,
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        IUsuarioRepository users,
        IHistoricoTarefaRepository history,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return ValidationError("body", "O corpo da requisição é obrigatório.");
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Titulo)
            || request.Titulo.Trim().Length > Tarefa.TituloMaxLength)
        {
            errors["titulo"] = [$"O título é obrigatório e deve ter até {Tarefa.TituloMaxLength} caracteres."];
        }

        if (!Enum.IsDefined(request.Prioridade))
        {
            errors["prioridade"] = ["Informe uma prioridade válida."];
        }

        if (request.ResponsavelId == Guid.Empty)
        {
            errors["responsavelId"] = ["O identificador do responsável não pode ser vazio."];
        }

        if (request.PrazoUtc.HasValue && request.PrazoUtc.Value.Kind != DateTimeKind.Utc)
        {
            errors["prazoUtc"] = ["O prazo deve ser informado em UTC."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var subject = principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var creatorId) || creatorId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (request.ResponsavelId is Guid responsavelId)
        {
            var responsavel = await users.GetByIdAsync(responsavelId, cancellationToken);
            if (responsavel is null)
            {
                return Results.NotFound(new { error = "O responsável informado não existe." });
            }

            if (responsavel.Perfil != PerfilUsuario.Funcionario)
            {
                return ValidationError("responsavelId", "O responsável deve ser um funcionário.");
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var task = new Tarefa(
            Guid.NewGuid(),
            request.Titulo!,
            request.Descricao,
            StatusTarefa.Pendente,
            request.Prioridade,
            now,
            request.PrazoUtc,
            creatorId,
            request.ResponsavelId);
        var creationHistory = new HistoricoTarefa(
            Guid.NewGuid(),
            task.Id,
            creatorId,
            "CriarTarefa",
            now,
            "Tarefa criada.");

        try
        {
            await tasks.AddAsync(task, cancellationToken);
            await history.AddAsync(creationHistory, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PersistenceConstraintException)
        {
            return Results.Conflict(new { error = "A tarefa viola uma restrição de integridade." });
        }

        var response = PublicTaskResponse.From(task);
        return Results.Created($"/api/tasks/{task.Id}", response);
    }

    private static async Task<IResult> GetByIdAsync(
        Guid id,
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ValidationError("id", "O identificador da tarefa é inválido.");
        }

        var task = await tasks.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        if (principal.IsInRole(nameof(PerfilUsuario.Administrador)))
        {
            return Results.Ok(PublicTaskResponse.From(task));
        }

        var subject = principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId)
            || task.CriadorId != userId && task.ResponsavelId != userId)
        {
            return Results.Forbid();
        }

        return Results.Ok(PublicTaskResponse.From(task));
    }

    private static async Task<IResult> GetHistoryAsync(
        Guid id,
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        IHistoricoTarefaRepository history,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        if (id == Guid.Empty)
        {
            return ValidationError("id", "O identificador da tarefa é inválido.");
        }

        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
        {
            return ValidationError("pagination", "A página deve estar entre 1 e 1000000 e pageSize entre 1 e 100.");
        }

        var task = await tasks.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        if (!principal.IsInRole(nameof(PerfilUsuario.Administrador)))
        {
            var subject = principal.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var userId)
                || task.CriadorId != userId && task.ResponsavelId != userId)
            {
                return Results.Forbid();
            }
        }

        var result = await history.ListPageByTarefaIdAsync(id, page, pageSize, cancellationToken);
        return Results.Ok(new PagedTaskHistoryResponse(
            result.History.Select(item => new PublicTaskHistoryResponse(
                item.Id,
                item.TarefaId,
                item.UsuarioId,
                item.Acao,
                item.DataUtc,
                item.Descricao)).ToArray(),
            page,
            pageSize,
            result.TotalCount));
    }

    private static IResult ValidationError(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
