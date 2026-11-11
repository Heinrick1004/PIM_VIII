using TaskFlow.Web.Contracts;

namespace TaskFlow.Web.Services;

public sealed class TaskFlowUserDirectory(ITaskFlowApiClient apiClient)
{
    public async Task<ApiResult<IReadOnlyDictionary<Guid, string>>> ResolveNamesAsync(
        string token,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new ApiResult<IReadOnlyDictionary<Guid, string>>(StatusCodes.Status200OK, new Dictionary<Guid, string>(), null);
        }

        var requests = ids.Select(id => apiClient.GetUserAsync(token, id, cancellationToken)).ToArray();
        var users = await Task.WhenAll(requests);
        var failure = users.FirstOrDefault(result => !result.Succeeded || result.Value is null);
        if (failure is not null)
        {
            return new ApiResult<IReadOnlyDictionary<Guid, string>>(failure.StatusCode, null, failure.Message);
        }

        IReadOnlyDictionary<Guid, string> names = users
            .Select(result => result.Value!)
            .ToDictionary(user => user.Id, user => user.Nome);
        return new ApiResult<IReadOnlyDictionary<Guid, string>>(StatusCodes.Status200OK, names, null);
    }
}
