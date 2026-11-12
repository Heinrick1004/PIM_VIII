using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Web.Contracts;

namespace TaskFlow.Web.Services;

public sealed class TaskFlowApiClient(HttpClient httpClient) : ITaskFlowApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<LoginApiResponse>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken) =>
        SendAsync<LoginApiResponse>(
            HttpMethod.Post,
            "/api/auth/login",
            new LoginApiRequest(email, password),
            null,
            cancellationToken);

    public Task<ApiResult<PublicUser>> GetCurrentUserAsync(
        string token,
        CancellationToken cancellationToken) =>
        SendAsync<PublicUser>(HttpMethod.Get, "/api/auth/me", null, token, cancellationToken);

    public Task<ApiResult<PagedTasks>> GetTasksAsync(
        string token,
        int page,
        int pageSize,
        StatusTarefa? status,
        PrioridadeTarefa? priority,
        Guid? responsibleId,
        bool withoutResponsible,
        string? title,
        CancellationToken cancellationToken)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (status.HasValue)
        {
            query.Add($"status={(int)status.Value}");
        }

        if (priority.HasValue)
        {
            query.Add($"prioridade={(int)priority.Value}");
        }

        if (responsibleId.HasValue)
        {
            query.Add($"responsavelId={Uri.EscapeDataString(responsibleId.Value.ToString())}");
        }

        if (withoutResponsible)
        {
            query.Add("semResponsavel=true");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            query.Add($"titulo={Uri.EscapeDataString(title.Trim())}");
        }

        return SendAsync<PagedTasks>(
            HttpMethod.Get,
            $"/api/tasks?{string.Join("&", query)}",
            null,
            token,
            cancellationToken);
    }

    public Task<ApiResult<PublicTask>> GetTaskAsync(
        string token,
        Guid id,
        CancellationToken cancellationToken) =>
        SendAsync<PublicTask>(HttpMethod.Get, $"/api/tasks/{id}", null, token, cancellationToken);

    public Task<ApiResult<PagedTaskHistory>> GetTaskHistoryAsync(
        string token,
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        SendAsync<PagedTaskHistory>(
            HttpMethod.Get,
            $"/api/tasks/{id}/history?page={page}&pageSize={pageSize}",
            null,
            token,
            cancellationToken);

    public Task<ApiResult<PublicTask>> CreateTaskAsync(
        string token,
        CreateTaskApiRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<PublicTask>(HttpMethod.Post, "/api/tasks", request, token, cancellationToken);

    public Task<ApiResult<PublicTask>> UpdateTaskAsync(
        string token,
        Guid id,
        UpdateTaskApiRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<PublicTask>(HttpMethod.Put, $"/api/tasks/{id}", request, token, cancellationToken);

    public Task<ApiResult<PublicTask>> UpdateTaskStatusAsync(
        string token,
        Guid id,
        UpdateTaskStatusApiRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<PublicTask>(HttpMethod.Patch, $"/api/tasks/{id}/status", request, token, cancellationToken);

    public Task<ApiResult<PagedUsers>> GetUsersAsync(
        string token,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        SendAsync<PagedUsers>(
            HttpMethod.Get,
            $"/api/users?page={page}&pageSize={pageSize}",
            null,
            token,
            cancellationToken);

    public Task<ApiResult<PublicUser>> GetUserAsync(
        string token,
        Guid id,
        CancellationToken cancellationToken) =>
        SendAsync<PublicUser>(HttpMethod.Get, $"/api/users/{id}", null, token, cancellationToken);

    public Task<ApiResult<PublicUser>> CreateUserAsync(
        string token,
        CreateUserApiRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<PublicUser>(HttpMethod.Post, "/api/users", request, token, cancellationToken);

    public Task<ApiResult<PublicUser>> UpdateUserAsync(
        string token,
        Guid id,
        UpdateUserApiRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<PublicUser>(HttpMethod.Put, $"/api/users/{id}", request, token, cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        string? token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T>(StatusCodes.Status503ServiceUnavailable, default, "A API TaskFlow está indisponível. Tente novamente.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ApiResult<T>(StatusCodes.Status503ServiceUnavailable, default, "A API TaskFlow demorou para responder. Tente novamente.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>(
                    (int)response.StatusCode,
                    default,
                    await ReadErrorMessageAsync(response, cancellationToken));
            }

            try
            {
                var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                return result is null
                    ? new ApiResult<T>(StatusCodes.Status502BadGateway, default, "A API retornou uma resposta inesperada.")
                    : new ApiResult<T>((int)response.StatusCode, result, null);
            }
            catch (JsonException)
            {
                return new ApiResult<T>(StatusCodes.Status502BadGateway, default, "A API retornou uma resposta inesperada.");
            }
        }
    }

    private static async Task<string> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if ((int)response.StatusCode >= 500)
        {
            return SafeMessage(response.StatusCode);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return SafeMessage(response.StatusCode);
            }
            foreach (var name in new[] { "detail", "error", "title" })
            {
                if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var message = value.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message;
                    }
                }
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in errors.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var value in property.Value.EnumerateArray())
                    {
                        if (value.ValueKind == JsonValueKind.String
                            && !string.IsNullOrWhiteSpace(value.GetString()))
                        {
                            return value.GetString()!;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            return SafeMessage(response.StatusCode);
        }

        return SafeMessage(response.StatusCode);
    }

    private static string SafeMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Revise os dados informados.",
        HttpStatusCode.Unauthorized => "Sua sessão expirou. Entre novamente.",
        HttpStatusCode.Forbidden => "Você não tem permissão para realizar esta ação.",
        HttpStatusCode.NotFound => "O registro solicitado não foi encontrado.",
        HttpStatusCode.Conflict => "Os dados entraram em conflito. Atualize a página e tente novamente.",
        (HttpStatusCode)429 => "Muitas tentativas. Aguarde um momento e tente novamente.",
        _ => "Não foi possível concluir a operação. Tente novamente."
    };
}
