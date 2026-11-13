using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
        group.MapGet("/", ListAsync);
        group.MapGet("/summary", SummaryAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/history", GetHistoryAsync);
        group.MapPatch("/{id:guid}/status", UpdateStatusAsync);
        group.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization("Administrator");
        return group;
    }

    private static async Task<IResult> SummaryAsync(
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId) || userId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var isAdministrator = principal.IsInRole(nameof(PerfilUsuario.Administrador));
        var summary = await tasks.GetSummaryAsync(userId, isAdministrator, cancellationToken);
        return Results.Ok(new TaskSummaryResponse(summary.Total, summary.Pending, summary.InProgress, summary.Completed));
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

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        CancellationToken cancellationToken,
        StatusTarefa? status = null,
        Guid? responsavelId = null,
        string? titulo = null,
        int page = 1,
        int pageSize = 20,
        bool semResponsavel = false,
        PrioridadeTarefa? prioridade = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
        {
            errors["pagination"] = ["A página deve estar entre 1 e 1000000 e pageSize entre 1 e 100."];
        }

        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            errors["status"] = ["Informe um status válido."];
        }

        if (prioridade.HasValue && !Enum.IsDefined(prioridade.Value))
        {
            errors["prioridade"] = ["Informe uma prioridade válida."];
        }

        if (responsavelId == Guid.Empty)
        {
            errors["responsavelId"] = ["O identificador do responsável não pode ser vazio."];
        }

        if (semResponsavel && responsavelId.HasValue)
        {
            errors["responsavelId"] = ["Não combine responsavelId com semResponsavel=true."];
        }

        if (titulo?.Length > Tarefa.TituloMaxLength)
        {
            errors["titulo"] = [$"O título deve ter até {Tarefa.TituloMaxLength} caracteres."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        Guid? visibleUserId = null;
        if (!principal.IsInRole(nameof(PerfilUsuario.Administrador)))
        {
            var subject = principal.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
            {
                return Results.Unauthorized();
            }

            visibleUserId = userId;
        }

        var result = await tasks.ListPageAsync(
            page,
            pageSize,
            visibleUserId,
            status,
            prioridade,
            responsavelId,
            semResponsavel,
            titulo,
            cancellationToken);
        return Results.Ok(new PagedTasksResponse(
            result.Tasks.Select(PublicTaskResponse.From).ToArray(),
            page,
            pageSize,
            result.TotalCount));
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

    private static async Task<IResult> UpdateStatusAsync(
        Guid id,
        UpdateTaskStatusRequest? request,
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        IHistoricoTarefaRepository history,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ValidationError("id", "O identificador da tarefa é inválido.");
        }

        if (request is null)
        {
            return ValidationError("body", "O corpo da requisição deve ser um objeto JSON.");
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return ValidationError("rowVersion", "A versão deve ser Base64 de exatamente 8 bytes.");
        }

        if (!TryReadEnum(request.Status, out StatusTarefa status))
        {
            return ValidationError("status", "Informe um status válido.");
        }

        var task = await tasks.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        var subject = principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var actorId) || actorId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        if (!principal.IsInRole(nameof(PerfilUsuario.Administrador)) && task.ResponsavelId != actorId)
        {
            return Results.Forbid();
        }

        if (!AreRowVersionsEqual(task.RowVersion, rowVersion))
        {
            return Results.Conflict(new { error = "A tarefa foi alterada por outra operação." });
        }

        if (task.Status == status)
        {
            return Results.Ok(PublicTaskResponse.From(task));
        }

        task.SetRowVersion(rowVersion);
        var now = clock.GetUtcNow().UtcDateTime;
        var historyEntry = task.AlterarStatus(status, actorId, now);
        if (historyEntry is null)
        {
            return Results.Ok(PublicTaskResponse.From(task));
        }

        await using var transaction = await unitOfWork.BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await tasks.UpdateAsync(task, cancellationToken);
            await history.AddAsync(historyEntry, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(PublicTaskResponse.From(task));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { error = "A tarefa foi alterada por outra operação." });
        }
        catch (PersistenceConstraintException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { error = "A tarefa viola uma restrição de integridade." });
        }
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateTaskRequest? request,
        ClaimsPrincipal principal,
        ITarefaRepository tasks,
        IUsuarioRepository users,
        IHistoricoTarefaRepository history,
        IUnitOfWork unitOfWork,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return ValidationError("id", "O identificador da tarefa é inválido.");
        }

        if (request is null)
        {
            return ValidationError("body", "O corpo da requisição é obrigatório.");
        }

        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
        {
            return ValidationError("rowVersion", "A versão deve ser Base64 de exatamente 8 bytes.");
        }

        if (!principal.IsInRole(nameof(PerfilUsuario.Administrador)))
        {
            return Results.Forbid();
        }

        var subject = principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var actorId) || actorId == Guid.Empty)
        {
            return Results.Unauthorized();
        }

        var task = await tasks.GetByIdAsync(id, cancellationToken);
        if (task is null)
        {
            return Results.NotFound();
        }

        if (!AreRowVersionsEqual(task.RowVersion, rowVersion))
        {
            return Results.Conflict(new { error = "A tarefa foi alterada por outra operação." });
        }

        if (HasValue(request.Status))
        {
            return ValidationError("status", "Altere o status pelo endpoint específico.");
        }

        var errors = new Dictionary<string, string[]>();
        var title = HasValue(request.Titulo) && TryReadOptionalString(request.Titulo, out var titleValue)
            ? titleValue
            : null;
        if (HasValue(request.Titulo) && title is null)
        {
            errors["titulo"] = ["O título é obrigatório."];
        }
        else if (title is not null && (title.Trim().Length > Tarefa.TituloMaxLength || string.IsNullOrWhiteSpace(title)))
        {
            errors["titulo"] = [$"O título é obrigatório e deve ter até {Tarefa.TituloMaxLength} caracteres."];
        }

        string? description = null;
        if (HasValue(request.Descricao))
        {
            if (!TryReadOptionalString(request.Descricao, out var descriptionValue))
            {
                errors["descricao"] = ["A descrição deve ser texto ou nulo."];
            }
            else
            {
                description = descriptionValue;
            }
        }

        var priority = default(PrioridadeTarefa?);
        if (HasValue(request.Prioridade))
        {
            if (!TryReadEnum(request.Prioridade, out PrioridadeTarefa parsedPriority))
            {
                errors["prioridade"] = ["Informe uma prioridade válida."];
            }
            else
            {
                priority = parsedPriority;
            }
        }

        var prazoUtc = default(DateTime?);
        if (HasValue(request.PrazoUtc))
        {
            if (!TryReadNullableDateTimeUtc(request.PrazoUtc, out var parsedPrazo))
            {
                errors["prazoUtc"] = ["O prazo deve estar em UTC."];
            }
            else
            {
                prazoUtc = parsedPrazo;
            }
        }

        Guid? responsavelId = null;
        if (HasValue(request.ResponsavelId))
        {
            if (!TryReadNullableGuid(request.ResponsavelId, out var parsedResponsavelId))
            {
                errors["responsavelId"] = ["O identificador do responsável não pode ser vazio."];
            }
            else
            {
                responsavelId = parsedResponsavelId;
                if (responsavelId == Guid.Empty)
                {
                    errors["responsavelId"] = ["O identificador do responsável não pode ser vazio."];
                }
            }
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (responsavelId is Guid candidateResponsavel && candidateResponsavel != Guid.Empty)
        {
            var responsavel = await users.GetByIdAsync(candidateResponsavel, cancellationToken);
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
        var events = new List<HistoricoTarefa>();
        if (title is not null)
        {
            var eventEntry = task.AlterarTitulo(title, actorId, now);
            if (eventEntry is not null)
            {
                events.Add(eventEntry);
            }
        }

        if (HasValue(request.Descricao))
        {
            var eventEntry = task.AlterarDescricao(description, actorId, now);
            if (eventEntry is not null)
            {
                events.Add(eventEntry);
            }
        }

        if (priority.HasValue)
        {
            var eventEntry = task.AlterarPrioridade(priority.Value, actorId, now);
            if (eventEntry is not null)
            {
                events.Add(eventEntry);
            }
        }

        if (HasValue(request.PrazoUtc))
        {
            var eventEntry = task.AlterarPrazo(prazoUtc, actorId, now);
            if (eventEntry is not null)
            {
                events.Add(eventEntry);
            }
        }

        if (HasValue(request.ResponsavelId))
        {
            var eventEntry = task.AlterarResponsavel(responsavelId, actorId, now);
            if (eventEntry is not null)
            {
                events.Add(eventEntry);
            }
        }

        if (events.Count == 0)
        {
            return Results.Ok(PublicTaskResponse.From(task));
        }

        task.SetRowVersion(rowVersion);
        await using var transaction = await unitOfWork.BeginSerializableTransactionAsync(cancellationToken);
        try
        {
            await tasks.UpdateAsync(task, cancellationToken);
            foreach (var eventEntry in events)
            {
                await history.AddAsync(eventEntry, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(PublicTaskResponse.From(task));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { error = "A tarefa foi alterada por outra operação." });
        }
        catch (PersistenceConstraintException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Results.Conflict(new { error = "A tarefa viola uma restrição de integridade." });
        }
    }

    private static bool HasValue(JsonElement value) =>
        value.ValueKind != JsonValueKind.Undefined;

    private static bool TryReadOptionalString(JsonElement property, out string? value)
    {
        if (property.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadNullableGuid(JsonElement property, out Guid? value)
    {
        if (property.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String
            && Guid.TryParse(property.GetString(), out var parsedGuid)
            && parsedGuid != Guid.Empty)
        {
            value = parsedGuid;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadNullableDateTimeUtc(JsonElement property, out DateTime? value)
    {
        if (property.ValueKind == JsonValueKind.Null)
        {
            value = null;
            return true;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        if (text is not null
            && (text.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("+00:00", StringComparison.Ordinal))
            && DateTimeOffset.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var parsedDate)
            && parsedDate.Offset == TimeSpan.Zero)
        {
            value = parsedDate.UtcDateTime;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadEnum<TEnum>(JsonElement property, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (Enum.TryParse<TEnum>(text, true, out var parsed) && Enum.IsDefined(parsed))
            {
                value = parsed;
                return true;
            }
        }
        else if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetByte(out var number))
            {
                var parsed = (TEnum)Enum.ToObject(typeof(TEnum), number);
                if (Enum.IsDefined(parsed))
                {
                    value = parsed;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseRowVersion(string? rowVersion, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            return false;
        }

        try
        {
            var decoded = Convert.FromBase64String(rowVersion);
            if (decoded.Length != 8)
            {
                return false;
            }

            bytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool AreRowVersionsEqual(byte[]? left, byte[]? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        return left.SequenceEqual(right);
    }

    private static IResult ValidationError(string key, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}
