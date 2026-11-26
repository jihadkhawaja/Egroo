using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private const int AgentTaskPageSize = 20;

    private async Task ShowAgentTasksAsync()
    {
        await CloseChannelViewAsync();
        SelectSection(WorkspaceSection.Agents);
        var panel = Page("Agent tasks", "Assign a one-time task to an agent you own.",
            IconButton("Back to agents", BackIcon, ShowAgentsAsync));
        var lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        panel.DetachedFromVisualTree += (_, _) =>
        {
            lifetime.Cancel();
            lifetime.Dispose();
        };

        var agents = new Dictionary<Guid, AgentDefinition>();
        var picker = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<AgentDefinition>((agent, _) => Text(agent?.Name, 14, Ink))
        };
        var prompt = Field("Describe the task");
        prompt.AcceptsReturn = true;
        prompt.TextWrapping = TextWrapping.Wrap;
        prompt.MinHeight = 92;
        prompt.MaxLength = CreateAgentTaskRequest.MaxPromptLength;
        var form = new StackPanel { Spacing = 12 };
        var unavailable = Text("Loading agents…", 14, Muted);
        var rows = new StackPanel { Spacing = 10 };
        var status = Text("Loading tasks…", 14, Muted);
        var displayed = new Dictionary<Guid, (AgentTaskInfo Task, Control View)>();
        var service = _session.AgentTasks;
        var offset = 0;
        var activeLoads = 0;
        var loadVersion = 0;
        CreateAgentTaskRequest? pending = null;

        Button? previous = null;
        Button? next = null;
        previous = QuietButton("Newer tasks", () => LoadPageAsync(Math.Max(0, offset - AgentTaskPageSize)));
        next = QuietButton("Older tasks", () => LoadPageAsync(offset + AgentTaskPageSize));
        previous.IsVisible = next.IsVisible = false;
        var history = new StackPanel { Spacing = 12 };
        history.Children.Add(Actions(Text("Task history", 18, Ink, FontWeight.SemiBold),
            QuietButton("Refresh", () => LoadPageAsync(offset))));
        history.Children.Add(status);
        history.Children.Add(rows);
        history.Children.Add(Actions(previous, next));

        var submit = PrimaryButton("Assign task", SubmitAsync);
        submit.IsEnabled = false;
        form.Children.Add(new StackPanel
        {
            Spacing = 6,
            Children = { LabeledField("Agent", picker), unavailable, LabeledField("Task", prompt) }
        });
        form.Children.Add(submit);
        panel.Children.Add(Card(form));
        panel.Children.Add(Card(history));

        void UpdateSubmit() =>
            submit.IsEnabled = picker.SelectedItem is AgentDefinition && !string.IsNullOrWhiteSpace(prompt.Text);
        picker.SelectionChanged += (_, _) => UpdateSubmit();
        prompt.TextChanged += (_, _) => UpdateSubmit();

        try
        {
            var owned = await _session.Agents.GetAgents(token) ?? [];
            if (token.IsCancellationRequested)
                return;
            foreach (var agent in owned.Where(agent => agent.UserId == _session.User?.Id))
                agents[agent.Id] = agent;
            var available = agents.Values.Where(agent => agent.IsActive).OrderBy(agent => agent.Name).ToArray();
            picker.ItemsSource = available;
            picker.SelectedIndex = available.Length == 0 ? -1 : 0;
            unavailable.Text = "Create or activate one of your agents to assign a task.";
            unavailable.IsVisible = available.Length == 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            unavailable.Text = "Couldn’t load your agents. Reopen this page to retry.";
        }

        await LoadPageAsync(0);
        _ = PollTasksAsync();

        async Task SubmitAsync()
        {
            if (picker.SelectedItem is not AgentDefinition agent || string.IsNullOrWhiteSpace(prompt.Text))
                return;
            var text = prompt.Text.Trim();
            if (pending is null || pending.AgentId != agent.Id || pending.Prompt != text)
                pending = new CreateAgentTaskRequest(agent.Id, Guid.NewGuid(), text);
            try
            {
                await service.CreateAsync(pending, token);
                pending = null;
                if (string.Equals(prompt.Text?.Trim(), text, StringComparison.Ordinal))
                    prompt.Text = string.Empty;
                await LoadPageAsync(0);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        async Task CancelAsync(Guid taskId)
        {
            try
            {
                var task = await service.CancelAsync(taskId, token);
                if (!token.IsCancellationRequested && task.Status != AgentTaskStatus.Cancelled)
                    Notify("The task finished before it could be cancelled.");
                await LoadPageAsync(offset);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        async Task LoadPageAsync(int requestedOffset)
        {
            if (token.IsCancellationRequested)
                return;
            var version = ++loadVersion;
            activeLoads++;
            try
            {
                var tasks = await service.ListAsync(requestedOffset, AgentTaskPageSize + 1, token);
                if (token.IsCancellationRequested || version != loadVersion)
                    return;

                offset = requestedOffset;
                previous!.IsVisible = offset > 0;
                next!.IsVisible = tasks.Length > AgentTaskPageSize;
                var nextRows = new List<Control>(AgentTaskPageSize);
                var current = new Dictionary<Guid, (AgentTaskInfo, Control)>();
                foreach (var task in tasks.Take(AgentTaskPageSize))
                {
                    var view = displayed.TryGetValue(task.Id, out var row) && row.Task == task
                        ? row.View
                        : AgentTaskRow(task, agents.GetValueOrDefault(task.AgentId)?.Name, CancelAsync);
                    current[task.Id] = (task, view);
                    nextRows.Add(view);
                }

                displayed = current;
                ApplyTimelineRows(rows.Children, nextRows);
                status.Text = "No tasks on this page.";
                status.IsVisible = nextRows.Count == 0;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException)
            {
                if (version == loadVersion)
                {
                    status.Text = "Couldn’t refresh your tasks. Try Refresh.";
                    status.IsVisible = true;
                }
            }
            finally
            {
                activeLoads--;
            }
        }

        async Task PollTasksAsync()
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    if (activeLoads == 0 && displayed.Values.Any(row =>
                            row.Task.Status is AgentTaskStatus.Queued or AgentTaskStatus.Running))
                    {
                        await LoadPageAsync(offset);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }
    }

    private Control AgentTaskRow(AgentTaskInfo task, string? agentName, Func<Guid, Task> cancel)
    {
        var description = task.Prompt.Split('\n', 2)[0].Trim();
        if (description.Length > 100)
            description = description[..100] + "…";
        var summary = new StackPanel
        {
            Spacing = 5,
            Children =
            {
                Text(description, 15, Ink, FontWeight.SemiBold),
                Text($"{agentName ?? "Agent unavailable"} · {task.CreatedAt.ToLocalTime():g}", 13, Muted)
            }
        };
        var state = task.Status switch
        {
            AgentTaskStatus.Queued => "Queued",
            AgentTaskStatus.Running => "Running",
            AgentTaskStatus.Completed => "Completed",
            AgentTaskStatus.Cancelled => "Cancelled",
            AgentTaskStatus.Failed => task.Failure switch
            {
                AgentTaskFailure.AgentUnavailable => "Failed · Agent unavailable",
                AgentTaskFailure.Interrupted => "Failed · Execution interrupted",
                _ => "Failed · Agent couldn’t complete the task"
            },
            _ => "Unknown status"
        };
        var details = new StackPanel { Spacing = 8 };
        details.Children.Add(TaskDetailsField("Task", task.Prompt));
        if (task.Status == AgentTaskStatus.Completed)
            details.Children.Add(TaskDetailsField("Result", task.Result ?? string.Empty));
        var actions = Actions(Text(state, 13, Ink));
        if (task.Status is AgentTaskStatus.Queued or AgentTaskStatus.Running)
            actions.Children.Add(QuietButton("Cancel task", () => cancel(task.Id)));
        return Card(new StackPanel
        {
            Spacing = 8,
            Children = { summary, actions, new Expander { Header = "Details", Content = details } }
        }, 14);
    }

    private static StackPanel TaskDetailsField(string label, string value)
    {
        var field = new TextBox
        {
            Text = value, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 280
        };
        return LabeledField(label, field);
    }
}
