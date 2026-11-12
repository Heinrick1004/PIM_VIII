using TaskFlow.Web.Contracts;

namespace TaskFlow.Web.Services;

public interface ITaskFlowApiClient
{
    Task<ApiResult<LoginApiResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<ApiResult<PublicUser>> GetCurrentUserAsync(string token, CancellationToken cancellationToken);

    Task<ApiResult<PagedTasks>> GetTasksAsync(
        string token,
        int page,
        int pageSize,
        StatusTarefa? status,
        PrioridadeTarefa? priority,
        Guid? responsibleId,
        bool withoutResponsible,
        string? title,
        CancellationToken cancellationToken);

    Task<ApiResult<PublicTask>> GetTaskAsync(string token, Guid id, CancellationToken cancellationToken);

    Task<ApiResult<PagedTaskHistory>> GetTaskHistoryAsync(
        string token,
        Guid id,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApiResult<PublicTask>> CreateTaskAsync(
        string token,
        CreateTaskApiRequest request,
        CancellationToken cancellationToken);

    Task<ApiResult<PublicTask>> UpdateTaskAsync(
        string token,
        Guid id,
        UpdateTaskApiRequest request,
        CancellationToken cancellationToken);

    Task<ApiResult<PublicTask>> UpdateTaskStatusAsync(
        string token,
        Guid id,
        UpdateTaskStatusApiRequest request,
        CancellationToken cancellationToken);

    Task<ApiResult<PagedUsers>> GetUsersAsync(
        string token,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApiResult<PublicUser>> GetUserAsync(string token, Guid id, CancellationToken cancellationToken);

    Task<ApiResult<PublicUser>> CreateUserAsync(
        string token,
        CreateUserApiRequest request,
        CancellationToken cancellationToken);

    Task<ApiResult<PublicUser>> UpdateUserAsync(
        string token,
        Guid id,
        UpdateUserApiRequest request,
        CancellationToken cancellationToken);
}
