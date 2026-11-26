using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private Task ShowAgentsAsync() => ShowAgentsAsync(null, false);

    private async Task ShowAgentsAsync(string? query, bool discover)
    {
        await CloseChannelViewAsync();
        SelectSection(WorkspaceSection.Agents);
        var panel = Page("Agents", "Create an agent, start a conversation, or discover agents shared by others.",
            IconButton("New agent", PlusIcon, () => EditAgentAsync(null), "primary"));
        var filters = new StackPanel { Spacing = 12 };
        filters.Children.Add(Actions(StyledButton("My agents", ShowAgentsAsync, discover ? "quiet" : "selected"),
            StyledButton("Discover", () => ShowAgentsAsync(null, true), discover ? "selected" : "quiet"),
            QuietButton("Tasks", ShowAgentTasksAsync)));
        if (discover)
        {
            var search = Field("Search published agents", query);
            Task SearchAsync() => ShowAgentsAsync(search.Text?.Trim(), true);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            var searchButton = IconButton("Search", SearchIcon, SearchAsync);
            row.Children.Add(search);
            Grid.SetColumn(searchButton, 1);
            row.Children.Add(searchButton);
            filters.Children.Add(row);
            SubmitOnEnter(search, SearchAsync);
        }
        panel.Children.Add(Card(filters, 14));
        var service = _session.Agents;
        var agents = discover ? await service.SearchPublishedAgents(query) ?? [] : await GetSavedAgentsAsync();
        foreach (var agent in agents)
        {
            var owned = agent.UserId == _session.User?.Id;
            var chat = PrimaryButton("Open conversations", () => ShowConversationsAsync(agent));
            Button option;
            if (owned)
                option = Button("Edit", () => EditAgentAsync(agent.Id));
            else
            {
                var saved = await service.IsAgentFriend(agent.Id);
                option = Button(saved ? "Remove saved agent" : "Save agent", async () =>
                {
                    await RequireAsync(saved ? service.RemoveAgentFriend(agent.Id) : service.AddAgentFriend(agent.Id));
                    await ShowAgentsAsync(query, discover);
                });
            }
            panel.Children.Add(RecordCard(RecordTitle(BadgeIcon(AgentIcon), agent.Name, agent.Description), chat, option));
        }
        if (agents.Length == 0)
            panel.Children.Add(Card(discover
                ? EmptyState(AgentIcon, "No agents found", "Try a different search or create an agent for your workspace.")
                : EmptyState(AgentIcon, "No agents yet", "Create an agent or discover agents published by other people.",
                    PrimaryButton("Create an agent", () => EditAgentAsync(null)), QuietButton("Browse agents", () => ShowAgentsAsync(null, true))), 0));
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
        var panel = Page(id.HasValue ? agent.Name : "Create agent", null, IconButton("Back to agents", BackIcon, ShowAgentsAsync));
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
        instructions.TextWrapping = TextWrapping.Wrap;
        var identity = SettingsGroup(panel, "Identity and instructions");
        identity.Children.Add(Actions(LabeledField("Name", name), LabeledField("Description", description), active));
        identity.Children.Add(LabeledField("Instructions", instructions));
        identity.Children.Add(LabeledField("Skills instruction prompt", prompt));
        var modelSettings = SettingsGroup(panel, "Model settings");
        modelSettings.Children.Add(Actions(LabeledField("Provider", provider), LabeledField("Model", model),
            LabeledField("Endpoint", endpoint), LabeledField("API key", apiKey)));
        modelSettings.Children.Add(Actions(LabeledField("Temperature", temperature), LabeledField("Max tokens", maxTokens)));
        var access = SettingsGroup(panel, "Access");
        access.Children.Add(LabeledField("Who can add this agent", permission));
        panel.Children.Add(PrimaryButton("Save agent", async () =>
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
        access.Children.Add(Button(agent.IsPublished ? "Unpublish" : "Publish", async () =>
        {
            await RequireAsync(agent.IsPublished ? service.UnpublishAgent(agent.Id) : service.PublishAgent(agent.Id));
            await EditAgentAsync(agent.Id);
        }));
        var resources = SettingsGroup(panel, "Resources");
        resources.Children.Add(Actions(Button("Skills and knowledge", () => ShowAgentResourcesAsync(agent)),
            Button("Tools and MCP", () => ShowAgentToolsAsync(agent))));
        AddConfirmation(access, "Delete agent", async () =>
        {
            await RequireAsync(service.DeleteAgent(agent.Id));
            await ShowAgentsAsync();
        });
    }

    private async Task ShowAgentResourcesAsync(AgentDefinition agent)
    {
        var panel = Page("Skills and knowledge", agent.Name, IconButton("Back to agent", BackIcon, () => EditAgentAsync(agent.Id)));
        var skills = SettingsGroup(panel, "Skills");
        var service = _session.Agents;
        foreach (var skill in await service.GetSkillDirectories(agent.Id) ?? [])
        {
            var name = Field("Skill name", skill.Name);
            var path = Field("Skill path", skill.Path);
            var enabled = new CheckBox { Content = "Enabled", IsChecked = skill.IsEnabled };
            skills.Children.Add(Actions(LabeledField("Name", name), LabeledField("Path", path), enabled, Button("Save skill", async () =>
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
        skills.Children.Add(Actions(LabeledField("Skill name", skillName), LabeledField("Server directory", skillPath), Button("Add directory", async () =>
        {
            if (await service.AddSkillDirectory(agent.Id, new AgentSkillDirectory { Name = skillName.Text ?? "", Path = skillPath.Text ?? "" }) is null)
                throw new InvalidOperationException("Could not add the directory.");
            await ShowAgentResourcesAsync(agent);
        })));
        skills.Children.Add(LabeledField("Managed skill content", skillContent));
        skills.Children.Add(Button("Add managed skill", async () =>
        {
            if (await service.AddManagedSkill(agent.Id, skillName.Text ?? "", skillContent.Text ?? "") is null)
                throw new InvalidOperationException("Could not create the skill.");
            await ShowAgentResourcesAsync(agent);
        }));

        var knowledge = SettingsGroup(panel, "Knowledge");
        foreach (var entry in (await service.GetKnowledge(agent.Id) ?? []).Append(new AgentKnowledge()))
        {
            var existing = entry.AgentDefinitionId != Guid.Empty;
            var title = Field("Knowledge title", entry.Title);
            var content = Field("Knowledge content", entry.Content);
            content.AcceptsReturn = true;
            var enabled = new CheckBox { Content = "Enabled", IsChecked = entry.IsEnabled };
            knowledge.Children.Add(Actions(LabeledField("Title", title), enabled));
            knowledge.Children.Add(LabeledField("Content", content));
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
            knowledge.Children.Add(controls);
        }
    }

    private async Task ShowAgentToolsAsync(AgentDefinition agent)
    {
        var service = _session.Agents;
        var panel = Page("Tools and MCP", agent.Name, IconButton("Back to agent", BackIcon, () => EditAgentAsync(agent.Id)));
        var tools = SettingsGroup(panel, "Tools");
        tools.Children.Add(Button("Add built-in tools", async () =>
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
            tools.Children.Add(Actions(enabled, Button("Remove", async () =>
            {
                await RequireAsync(service.DeleteTool(tool.Id));
                await ShowAgentToolsAsync(agent);
            })));
        }
        var servers = SettingsGroup(panel, "MCP servers");
        foreach (var server in (await service.GetMcpServers(agent.Id) ?? []).Append(new AgentMcpServer()))
        {
            var existing = server.AgentDefinitionId != Guid.Empty;
            var name = Field("MCP server name", server.Name);
            var endpoint = Field("HTTPS endpoint", server.Endpoint);
            var key = Field("API key", secret: true);
            var enabled = new CheckBox { Content = "Active", IsChecked = server.IsActive };
            var row = Actions(LabeledField("Name", name), LabeledField("HTTPS endpoint", endpoint),
                LabeledField("API key", key), enabled, Button(existing ? "Save server" : "Add server", async () =>
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
            servers.Children.Add(row);
        }
    }

    private async Task ShowConversationsAsync(AgentDefinition agent)
    {
        SelectSection(WorkspaceSection.Agents);
        var panel = Page("Conversations", agent.Name, IconButton("Back to agents", BackIcon, ShowAgentsAsync));
        var service = _session.Agents;
        var title = Field("Give it a name");
        var create = SettingsGroup(panel, "New conversation");
        create.Children.Add(LabeledField("Conversation name (optional)", title));
        async Task StartAsync()
        {
            var conversation = await service.CreateConversation(agent.Id, title.Text);
            if (conversation is null)
                throw new InvalidOperationException("Could not start a conversation.");
            await OpenAgentConversationAsync(agent, conversation);
        }
        create.Children.Add(IconButton("Start conversation", PlusIcon, StartAsync, "primary"));
        SubmitOnEnter(title, StartAsync);
        var conversations = await service.GetConversations(agent.Id) ?? [];
        foreach (var conversation in conversations)
        {
            var name = conversation.Title ?? "Conversation";
            panel.Children.Add(RecordCard(RecordTitle(BadgeIcon(ChatIcon), name),
                QuietButton("Open conversation", () => OpenAgentConversationAsync(agent, conversation))));
        }
        if (conversations.Length == 0)
        {
            panel.Children.Add(Card(EmptyState(ChatIcon, "No conversations yet", $"Start a conversation with {agent.Name} above."), 0));
            return;
        }
        AddConfirmation(panel, "Delete all conversations", async () =>
        {
            await service.DeleteAllConversations(agent.Id);
            await ShowConversationsAsync(agent);
        });
    }

    private async Task OpenAgentConversationAsync(AgentDefinition agent, AgentConversation conversation, int take = 50)
    {
        SelectSection(WorkspaceSection.Agents);
        var service = _session.Agents;
        var header = ConversationHeading(conversation.Title ?? agent.Name, "conversations", () => ShowConversationsAsync(agent),
            BadgeIcon(AgentIcon, TokenSurface, AccentInk), $"{agent.Name} · Agent");
        var more = MenuButton("More", ("Older messages", () => OpenAgentConversationAsync(agent, conversation, take + 50)),
            ("Clear memory", () => RequireAsync(service.ClearConversationMemory(conversation.Id))));
        var flyout = (Flyout)more.Flyout!;
        AddConfirmation((StackPanel)flyout.Content!, "Delete conversation", async () =>
        {
            flyout.Hide();
            await RequireAsync(service.DeleteConversation(conversation.Id));
            await ShowConversationsAsync(agent);
        });
        header.Children.Add(more);
        var items = new StackPanel { Spacing = 12 };
        var userId = _session.User!.Id;
        Border AgentBubble(string role, string content, DateTimeOffset? sent = null)
        {
            var isUser = role == "user";
            return CreateMessageBubble(new Message
            {
                SenderId = isUser ? userId : Guid.Empty,
                AgentDefinitionId = isUser ? null : agent.Id,
                DisplayName = isUser ? "You" : agent.Name, DateSent = sent ?? DateTimeOffset.UtcNow
            }, content, userId);
        }

        AgentChatAttachment? attachment = null;
        var attachmentStatus = Text(null, 12, AccentInk);
        var input = ComposerField();
        var empty = EmptyState(AgentIcon, "Start a conversation", $"Write a message to {agent.Name} below.");
        ScrollViewer? timeline = null;
        var remove = new Button { Content = "Remove attachment", Classes = { "workspace-action", "quiet" } };
        remove.Click += (_, _) =>
        {
            attachment = null;
            UpdateAttachment();
        };
        SubmitMessageOnEnter(input, SendAsync);
        var footer = ComposerPanel(input, SendAsync, attachmentStatus,
            GlyphButton("Attach a file", AttachmentIcon, SelectAttachmentAsync), remove);
        UpdateAttachment();
        timeline = ShowConversation(header, items, footer);
        var history = (await service.GetMessages(conversation.Id, take: take) ?? []).OrderBy(item => item.DateCreated).ToArray();
        foreach (var message in history)
            items.Children.Add(AgentBubble(message.Role, message.Content, message.DateCreated));
        if (history.Length == 0)
            items.Children.Add(empty);
        ScrollToLatest(timeline);

        async Task SendAsync()
        {
            var text = input.Text?.Trim() ?? "";
            if (text.Length == 0 && attachment is null)
                return;
            items.Children.Remove(empty);
            items.Children.Add(AgentBubble("user", attachment is null ? text : $"{text}\n{attachment.FileName}"));
            input.Text = null;
            var request = new AgentChatRequest { Message = text, Attachments = attachment is null ? null : [attachment] };
            attachment = null;
            UpdateAttachment();
            var preview = Text($"{agent.Name} is responding…", 14, Muted);
            var progress = Card(new StackPanel
            {
                Spacing = 6, Children = { Text(agent.Name, 13, Ink, FontWeight.SemiBold), preview }
            }, 12);
            progress.Background = AgentSurface;
            progress.HorizontalAlignment = HorizontalAlignment.Left;
            progress.MaxWidth = 650;
            items.Children.Add(progress);
            ScrollToLatest(timeline);
            var response = new StringBuilder();
            try
            {
                await foreach (var chunk in service.ChatStream(conversation.Id, request))
                {
                    response.Append(chunk);
                    preview.Text = response.ToString();
                    ScrollToLatest(timeline);
                }
                items.Children.Add(AgentBubble("assistant", response.ToString()));
            }
            finally
            {
                items.Children.Remove(progress);
                ScrollToLatest(timeline);
            }
        }

        async Task SelectAttachmentAsync()
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
            UpdateAttachment();
        }

        void UpdateAttachment()
        {
            attachmentStatus.Text = attachment?.FileName;
            attachmentStatus.IsVisible = attachment is not null;
            remove.IsVisible = attachment is not null;
        }
    }

    private static ComboBox EnumField<T>(T selected) where T : struct, Enum => new()
    {
        ItemsSource = Enum.GetValues<T>(), SelectedItem = selected, Classes = { "workspace-field" }
    };
}
