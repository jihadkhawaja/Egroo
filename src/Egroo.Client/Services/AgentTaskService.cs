using System.Net;
using System.Net.Http.Json;
using Egroo.Contracts.Models;

namespace Egroo.Client.Services;

public sealed class AgentTaskService(HttpClient http)
{
    private const string BasePath = "api/v1/AgentTasks/";

    public async Task<AgentTaskInfo[]> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var path = FormattableString.Invariant($"{BasePath}?skip={skip}&take={take}");
        return await http.GetFromJsonAsync<AgentTaskInfo[]>(path, cancellationToken)
            ?? throw new InvalidOperationException("The server did not return your tasks.");
    }

    public async Task<AgentTaskInfo> CreateAsync(CreateAgentTaskRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(BasePath, request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("The selected agent is unavailable.");
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("Task submissions are temporarily limited. Wait for a task to finish or cancel one and try again.");

        response.EnsureSuccessStatusCode();
        return await ReadTaskAsync(response, cancellationToken);
    }

    public async Task<AgentTaskInfo> CancelAsync(Guid taskId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"{BasePath}{taskId:D}/cancel", null, cancellationToken);
        // A task may finish while the cancellation request reaches the server.
        if (response.StatusCode != HttpStatusCode.Conflict)
            response.EnsureSuccessStatusCode();
        return await ReadTaskAsync(response, cancellationToken);
    }

    private static async Task<AgentTaskInfo> ReadTaskAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<AgentTaskInfo>(cancellationToken)
        ?? throw new InvalidOperationException("The server did not return the task.");
}
