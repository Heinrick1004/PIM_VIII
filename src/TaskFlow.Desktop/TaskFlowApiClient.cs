using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.Desktop;

internal sealed class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

internal sealed class TaskFlowApiClient : IDisposable
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public TaskFlowApiClient(Uri baseUri, HttpMessageHandler? handler = null) =>
        _http = handler is null ? new HttpClient { BaseAddress = baseUri } : new HttpClient(handler) { BaseAddress = baseUri };

    public async Task<LoginResponse> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password), Json, ct);
        return await ReadAsync<LoginResponse>(response, ct);
    }

    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(string token, CancellationToken ct = default)
    {
        var all = new List<TaskDto>();
        for (var page = 1; ; page++)
        {
            using var request = Authorized(HttpMethod.Get, $"api/tasks?page={page}&pageSize=100", token);
            using var response = await _http.SendAsync(request, ct);
            var result = await ReadAsync<PageDto<TaskDto>>(response, ct);
            all.AddRange(result.Items);
            if (all.Count >= result.TotalCount || result.Items.Count == 0) return all;
        }
    }

    public async Task<TaskDto> GetTaskAsync(string token, Guid id, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Get, $"api/tasks/{id}", token);
        using var response = await _http.SendAsync(request, ct);
        return await ReadAsync<TaskDto>(response, ct);
    }

    public async Task<IReadOnlyList<HistoryDto>> GetHistoryAsync(string token, Guid id, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Get, $"api/tasks/{id}/history?page=1&pageSize=100", token);
        using var response = await _http.SendAsync(request, ct);
        return (await ReadAsync<HistoryPageDto>(response, ct)).Items;
    }

    public async Task<TaskDto> UpdateStatusAsync(string token, Guid id, string status, string rowVersion, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Patch, $"api/tasks/{id}/status", token);
        request.Content = JsonContent.Create(new StatusRequest(status, rowVersion), options: Json);
        using var response = await _http.SendAsync(request, ct);
        return await ReadAsync<TaskDto>(response, ct);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<T>(Json, ct)) ?? throw new InvalidOperationException("Resposta vazia da API.");
        var body = await response.Content.ReadAsStringAsync(ct);
        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Dados inválidos. Confira os campos informados.",
            HttpStatusCode.Unauthorized => "Credenciais inválidas ou sessão expirada.",
            HttpStatusCode.Forbidden => "Seu usuário não tem permissão para esta operação.",
            HttpStatusCode.NotFound => "A tarefa não foi encontrada ou não está mais disponível.",
            HttpStatusCode.Conflict => "A tarefa foi alterada por outra operação. Os dados atuais foram recarregados.",
            _ => $"Falha na API ({(int)response.StatusCode}). {body}"
        };
        throw new ApiException(response.StatusCode, message);
    }

    public void Dispose() => _http.Dispose();
}
