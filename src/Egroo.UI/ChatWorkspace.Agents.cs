using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private Task ShowAgentsAsync() => ShowAgentsAsync(null, false);

    private async Task ShowAgentsAsync(string? query, bool discover)
    {
        _channel = null;
        var panel = Page("Agents");
        var search = Field("Search published agents", query);
        panel.Children.Add(Actions(Button("My agents", ShowAgentsAsync), Button("Create agent", () => EditAgentAsync(null)),
            search, Button("Discover", () => ShowAgentsAsync(search.Text, true))));
        var service = _session.Agents;
        var agents = discover ? await service.SearchPublishedAgents(query) ?? [] : await GetSavedAgentsAsync();
        foreach (var agent in agents)
        {
            var owned = agent.UserId == _session.User?.Id;
            var row = Actions(Label(agent.Name), Button("Open conversations", () => ShowConversationsAsync(agent)));
            if (owned)
                row.Children.Add(Button("Edit", () => EditAgentAsync(agent.Id)));
            else
            {
                var saved = await service.IsAgentFriend(agent.Id);
                row.Children.Add(Button(saved ? "Remove saved agent" : "Save agent", async () =>
                {
                    await RequireAsync(saved ? service.RemoveAgentFriend(agent.Id) : service.AddAgentFriend(agent.Id));
                    await ShowAgentsAsync(query, discover);
                }));
            }
            panel.Children.Add(row);
            if (!string.IsNullOrWhiteSpace(agent.Description))
                panel.Children.Add(Label(agent.Description));
        }
    }

    private async Task<AgentDefinition[]> GetSavedAgentsAsync()
    {
        var service = _session.Agents;
        var agents = new List<AgentDefinition>(await service.GetAgents() ?? []);
        foreach (var saved in await service.GetAgentFriends() ?? [])
        {
            if (await service.GetPublishedAgent(saved.AgentDefinitionId) is { } agent)
                agents.Add(agent);
        }
        return agents.DistinctBy(agent => agent.Id).ToArray();
    }

    private async Task EditAgentAsync(Guid? id)
    {
        var service = _session.Agents;
        var agent = id.HasValue ? await service.GetAgent(id.Value)
            ?? throw new InvalidOperationException("Could not load the agent.") : new AgentDefinition();
        var panel = Page(id.HasValue ? agent.Name : "Create agent");
        panel.Children.Add(Button("Back to agents", ShowAgentsAsync));
        var name = Field("Name", agent.Name);
        var description = Field("Description", agent.Description);
        var instructions = Field("Instructions", agent.Instructions);
        instructions.AcceptsReturn = true;
        instructions.MinHeight = 110;
        var prompt = Field("Skills instruction prompt", agent.SkillsInstructionPrompt);
        var provider = EnumField(agent.Provider);
        var model = Field("Model", agent.Model);
        var endpoint = Field("Endpoint", agent.Endpoint);
        var apiKey = Field("API key", secret: true);
        var temperature = Field("Temperature", agent.Temperature?.ToString(CultureInfo.InvariantCulture));
        var maxTokens = Field("Max tokens", agent.MaxTokens?.ToString(CultureInfo.InvariantCulture));
        var active = new CheckBox { Content = "Active", IsChecked = agent.IsActive };
        var permission = EnumField(agent.AddPermission);
        panel.Children.Add(Actions(name, description, active));
        panel.Children.Add(instructions);
        panel.Children.Add(prompt);
        panel.Children.Add(Actions(Label("Provider"), provider, model, endpoint, apiKey));
        panel.Children.Add(Actions(temperature, maxTokens, Label("Who can add this agent"), permission));
        panel.Children.Add(Button("Save agent", async () =>
        {
            agent.Name = name.Text?.Trim() ?? "";
            agent.Description = description.Text;
            agent.Instructions = instructions.Text;
            agent.SkillsInstructionPrompt = prompt.Text;
            agent.Provider = (LlmProvider)provider.SelectedItem!;
            agent.Model = model.Text?.Trim() ?? "";
            agent.Endpoint = endpoint.Text;
            agent.ApiKey = string.IsNullOrWhiteSpace(apiKey.Text) ? null : apiKey.Text;
            agent.IsActive = active.IsChecked == true;
            agent.AddPermission = (AgentAddPermission)permission.SelectedItem!;
            agent.Temperature = float.TryParse(temperature.Text, CultureInfo.InvariantCulture, out var heat) ? heat : null;
            agent.MaxTokens = int.TryParse(maxTokens.Text, CultureInfo.InvariantCulture, out var limit) ? limit : null;
            if (id.HasValue)
                await RequireAsync(service.UpdateAgent(agent));
            else
                agent = await service.CreateAgent(agent) ?? throw new InvalidOperationException("Could not create the agent.");
            await EditAgentAsync(agent.Id);
        }));
        if (!id.HasValue)
            return;
        panel.Children.Add(Actions(Button(agent.IsPublished ? "Unpublish" : "Publish", async () =>
        {
            await RequireAsync(agent.IsPublished ? service.UnpublishAgent(agent.Id) : service.PublishAgent(agent.Id));
            await EditAgentAsync(agent.Id);
        }), Button("Skills and knowledge", () => ShowAgentResourcesAsync(agent)),
            Button("Tools and MCP", () => ShowAgentToolsAsync(agent))));
        AddConfirmation(panel, "Delete agent", async () =>
        {
            await RequireAsync(service.DeleteAgent(agent.Id));
            await ShowAgentsAsync();
        });
    }

    private async Task ShowAgentResourcesAsync(AgentDefinition agent)
    {
        var panel = Page("Skills and knowledge");
        panel.Children.Add(Button("Back to agent", () => EditAgentAsync(agent.Id)));
        var service = _session.Agents;
        foreach (var skill in await service.GetSkillDirectories(agent.Id) ?? [])
        {
            var name = Field("Skill name", skill.Name);
            var path = Field("Skill path", skill.Path);
            var enabled = new CheckBox { Content = "Enabled", IsChecked = skill.IsEnabled };
            panel.Children.Add(Actions(name, path, enabled, Button("Save skill", async () =>
            {
                skill.Name = name.Text ?? "";
                skill.Path = path.Text ?? "";
                skill.IsEnabled = enabled.IsChecked == true;
                await RequireAsync(service.UpdateSkillDirectory(skill));
            }), Button("Remove skill", async () =>
            {
                await RequireAsync(service.DeleteSkillDirectory(skill.Id));
                await ShowAgentResourcesAsync(agent);
            })));
        }
        var skillName = Field("New skill name");
        var skillPath = Field("Server skill directory");
        var skillContent = Field("Managed SKILL.md content");
        skillContent.AcceptsReturn = true;
        panel.Children.Add(Actions(skillName, skillPath, Button("Add directory", async () =>
        {
            if (await service.AddSkillDirectory(agent.Id, new AgentSkillDirectory { Name = skillName.Text ?? "", Path = skillPath.Text ?? "" }) is null)
                throw new InvalidOperationException("Could not add the directory.");
            await ShowAgentResourcesAsync(agent);
        })));
        panel.Children.Add(skillContent);
        panel.Children.Add(Button("Add managed skill", async () =>
        {
            if (await service.AddManagedSkill(agent.Id, skillName.Text ?? "", skillContent.Text ?? "") is null)
                throw new InvalidOperationException("Could not create the skill.");
            await ShowAgentResourcesAsync(agent);
        }));

        foreach (var entry in (await service.GetKnowledge(agent.Id) ?? []).Append(new AgentKnowledge()))
        {
            var existing = entry.AgentDefinitionId != Guid.Empty;
            var title = Field("Knowledge title", entry.Title);
            var content = Field("Knowledge content", entry.Content);
            content.AcceptsReturn = true;
            var enabled = new CheckBox { Content = "Enabled", IsChecked = entry.IsEnabled };
            panel.Children.Add(Actions(title, enabled));
            panel.Children.Add(content);
            var controls = Actions(Button(existing ? "Save knowledge" : "Add knowledge", async () =>
            {
                entry.Title = title.Text ?? "";
                entry.Content = content.Text ?? "";
                entry.IsEnabled = enabled.IsChecked == true;
                if (existing)
                    await RequireAsync(service.UpdateKnowledge(entry));
                else if (await service.AddKnowledge(agent.Id, entry) is null)
                    throw new InvalidOperationException("Could not add knowledge.");
                await ShowAgentResourcesAsync(agent);
            }));
            if (existing)
                controls.Children.Add(Button("Remove knowledge", async () =>
                {
                    await RequireAsync(service.DeleteKnowledge(entry.Id));
                    await ShowAgentResourcesAsync(agent);
                }));
            panel.Children.Add(controls);
        }
    }

    private async Task ShowAgentToolsAsync(AgentDefinition agent)
    {
        var service = _session.Agents;
        var panel = Page("Tools and MCP");
        panel.Children.Add(Button("Back to agent", () => EditAgentAsync(agent.Id)));
        panel.Children.Add(Button("Add built-in tools", async () =>
        {
            await service.SeedBuiltinTools(agent.Id);
            await ShowAgentToolsAsync(agent);
        }));
        foreach (var tool in await service.GetTools(agent.Id) ?? [])
        {
            var enabled = new CheckBox { Content = tool.Name, IsChecked = tool.IsEnabled };
            enabled.IsCheckedChanged += (_, _) => _ = RunAsync(async () =>
            {
                tool.IsEnabled = enabled.IsChecked == true;
                await RequireAsync(service.UpdateTool(tool));
            });
            panel.Children.Add(Actions(enabled, Button("Remove", async () =>
            {
                await RequireAsync(service.DeleteTool(tool.Id));
                await ShowAgentToolsAsync(agent);
            })));
        }
        foreach (var server in (await service.GetMcpServers(agent.Id) ?? []).Append(new AgentMcpServer()))
        {
            var existing = server.AgentDefinitionId != Guid.Empty;
            var name = Field("MCP server name", server.Name);
            var endpoint = Field("HTTPS endpoint", server.Endpoint);
            var key = Field("API key", secret: true);
            var enabled = new CheckBox { Content = "Active", IsChecked = server.IsActive };
            var row = Actions(name, endpoint, key, enabled, Button(existing ? "Save server" : "Add server", async () =>
            {
                server.Name = name.Text ?? "";
                server.Endpoint = endpoint.Text ?? "";
                server.IsActive = enabled.IsChecked == true;
                if (existing)
                    await RequireAsync(service.UpdateMcpServer(server, key.Text));
                else if (await service.AddMcpServer(agent.Id, server.Name, server.Endpoint, key.Text) is null)
                    throw new InvalidOperationException("Could not add the server.");
                await ShowAgentToolsAsync(agent);
            }));
            if (existing)
            {
                row.Children.Add(Button("Discover tools", async () =>
                {
                    var result = await service.DiscoverMcpTools(server.Id);
                    Notify(result is null ? "Tool discovery failed." : $"Discovered {result.Discovered} tools.");
                    await ShowAgentToolsAsync(agent);
                }));
                row.Children.Add(Button("Remove server", async () =>
                {
                    await RequireAsync(service.DeleteMcpServer(server.Id));
                    await ShowAgentToolsAsync(agent);
                }));
            }
            panel.Children.Add(row);
        }
    }

    private async Task ShowConversationsAsync(AgentDefinition agent)
    {
        var panel = Page($"{agent.Name} · Conversations");
        panel.Children.Add(Button("Back to agents", ShowAgentsAsync));
        var service = _session.Agents;
        var title = Field("Conversation name");
        panel.Children.Add(Actions(title, Button("New conversation", async () =>
        {
            var conversation = await service.CreateConversation(agent.Id, title.Text);
            if (conversation is null)
                throw new InvalidOperationException("Could not start a conversation.");
            await OpenAgentConversationAsync(agent, conversation);
        })));
        foreach (var conversation in await service.GetConversations(agent.Id) ?? [])
            panel.Children.Add(Button(conversation.Title ?? "Conversation", () => OpenAgentConversationAsync(agent, conversation)));
        AddConfirmation(panel, "Delete all conversations", async () =>
        {
            await service.DeleteAllConversations(agent.Id);
            await ShowConversationsAsync(agent);
        });
    }

    private async Task OpenAgentConversationAsync(AgentDefinition agent, AgentConversation conversation, int take = 50)
    {
        var service = _session.Agents;
        var panel = Page(conversation.Title ?? agent.Name);
        panel.Children.Add(Actions(Button("Conversations", () => ShowConversationsAsync(agent)),
            Button("Older messages", () => OpenAgentConversationAsync(agent, conversation, take + 50)),
            Button("Clear memory", () => RequireAsync(service.ClearConversationMemory(conversation.Id)))));
        AddConfirmation(panel, "Delete conversation", async () =>
        {
            await RequireAsync(service.DeleteConversation(conversation.Id));
            await ShowConversationsAsync(agent);
        });
        var items = new StackPanel { Spacing = 8 };
        panel.Children.Add(items);
        var userId = _session.User!.Id;
        Border AgentBubble(string role, string content)
        {
            var isUser = role == "user";
            return CreateMessageBubble(new Message
            {
                SenderId = isUser ? userId : Guid.Empty,
                AgentDefinitionId = isUser ? null : agent.Id,
                DisplayName = isUser ? "You" : agent.Name
            }, content, userId);
        }
        foreach (var message in (await service.GetMessages(conversation.Id, take: take) ?? []).OrderBy(item => item.DateCreated))
            items.Children.Add(AgentBubble(message.Role, message.Content));

        var input = Field("Message");
        input.AcceptsReturn = true;
        panel.Children.Add(input);
        AgentChatAttachment? attachment = null;
        var attachmentStatus = Label(null);
        panel.Children.Add(attachmentStatus);
        panel.Children.Add(Actions(Button("Send", async () =>
        {
            var text = input.Text?.Trim() ?? "";
            if (text.Length == 0 && attachment is null)
                return;
            items.Children.Add(AgentBubble("user", attachment is null ? text : $"{text}\n{attachment.FileName}"));
            input.Text = null;
            var request = new AgentChatRequest { Message = text, Attachments = attachment is null ? null : [attachment] };
            attachment = null;
            attachmentStatus.Text = null;
            var progress = Label($"{agent.Name} is responding…");
            items.Children.Add(progress);
            var response = new System.Text.StringBuilder();
            try
            {
                await foreach (var chunk in service.ChatStream(conversation.Id, request))
                {
                    response.Append(chunk);
                    progress.Text = response.ToString();
                }
                items.Children.Add(AgentBubble("assistant", response.ToString()));
            }
            finally
            {
                items.Children.Remove(progress);
            }
        }), Button("Attach", async () =>
        {
            using var file = await PickFileAsync("Attach an image or document");
            if (file is null)
                return;
            var bytes = await ReadFileAsync(file, MaxAttachmentBytes);
            var mime = FileContentType(file.Name);
            attachment = new AgentChatAttachment
            {
                FileName = file.Name, ContentType = mime,
                DataUri = $"data:{mime};base64,{Convert.ToBase64String(bytes)}"
            };
            attachmentStatus.Text = file.Name;
        }), Button("Remove attachment", () =>
        {
            attachment = null;
            attachmentStatus.Text = null;
            return Task.CompletedTask;
        })));
    }

    private static ComboBox EnumField<T>(T selected) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(), SelectedItem = selected
    };
}
