using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Egroo.Contracts.Models;
using Egroo.UI.Helpers;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private const int MaxAttachmentBytes = 1024 * 1024 - 16;
    private static readonly Regex MessageTokens = new(
        @"\[\[egroo-file:(?<file>[A-Za-z0-9_-]+)\]\]|\[(?<label>[^\]\r\n]+)\]\((?<link>https?://[^)\s]+)\)|(?<url>https?://[^\s<>]+)|(?<![\w@])@(?<mention><[^>]+>|[\w.-]+)",
        RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(500));
    private static readonly IBrush SenderSurface = Brush("#E4E4E4");
    private static readonly IBrush SenderLine = Brush("#CDCDCD");
    private static readonly IBrush AgentSurface = Brush("#F1F1F1");
    private static readonly IBrush AgentLine = Brush("#BDBDBD");
    private static readonly IBrush TokenSurface = Brush("#DADADA");
    private readonly Dictionary<string, ChannelTypingState> _typing = new();
    private readonly Dictionary<Guid, (string Cipher, string Text)> _displayCache = new();
    private readonly DispatcherTimer _typingTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private StackPanel? _messages;
    private ScrollViewer? _timeline;
    private TextBlock? _typingStatus;
    private TextBlock? _voiceStatus;
    private Button? _voiceAction;
    private Button? _muteAction;
    private TextBox? _composer;
    private TextBox? _messageFilter;
    private IStorageFile? _attachment;
    private TextBlock? _attachmentStatus;
    private Button? _removeAttachment;
    private bool _isTyping;
    private int _messageLimit;

    private async Task OpenChannelAsync(Channel channel)
    {
        await CloseChannelViewAsync();
        SelectSection(WorkspaceSection.Channels);
        _channel = channel;
        _messageLimit = 150;

        var header = ConversationHeading(ChannelTitle(channel), "Channels", ShowChannelsAsync,
            BadgeIcon(ChatIcon), channel.IsPublic ? "Public channel" : "Private channel");
        _voiceStatus = Text(null, 12, Muted);
        _messageFilter = Field("Search this conversation");
        _messageFilter.IsVisible = false;
        AutomationProperties.SetName(_messageFilter, "Search messages");
        _messageFilter.TextChanged += (_, _) => _ = RenderMessagesSafelyAsync();
        var search = GlyphButton("Search messages", SearchIcon, () =>
        {
            _messageFilter.IsVisible = !_messageFilter.IsVisible;
            if (_messageFilter.IsVisible)
                Dispatcher.UIThread.Post(() => _messageFilter?.Focus(), DispatcherPriority.Loaded);
            else
                _messageFilter.Text = null;
            return Task.CompletedTask;
        });
        _voiceAction = Button("Join voice", async () =>
        {
            if (_session.Voice.IsCurrentChannel(channel.Id))
                await _session.Voice.LeaveCallAsync();
            else
                await _session.Voice.JoinCallAsync(channel.Id, ChannelTitle(channel));
        });
        _muteAction = Button("Mute", () => _session.Voice.ToggleMuteAsync());
        var more = MenuButton("More", ("Older messages", async () => { _messageLimit += 150; await RenderMessagesAsync(); }),
            ("Audio settings", () => ShowAudioSettingsAsync(channel)));
        header.Children.Add(Actions(search, QuietButton("Members", () => ShowMembersAsync(channel)), _voiceAction, _muteAction, more));
        header.Children.Add(_messageFilter);
        header.Children.Add(_voiceStatus);

        _messages = new StackPanel { Spacing = 12 };
        _composer = ComposerField();
        SubmitMessageOnEnter(_composer, SendMessageAsync);
        _composer.TextChanged += (_, _) => _ = UpdateComposerTypingAsync();
        _typingTimer.Stop();
        _typingTimer.Tick -= TypingTimerElapsed;
        _typingTimer.Tick += TypingTimerElapsed;
        _typingStatus = Text(null, 12, Muted);
        _typingStatus.IsVisible = false;
        _attachmentStatus = Text(null, 12, AccentInk);
        _attachmentStatus.IsVisible = false;
        _removeAttachment = QuietButton("Remove attachment", () =>
        {
            _attachment?.Dispose();
            _attachment = null;
            UpdateAttachmentStatus();
            return Task.CompletedTask;
        });
        _removeAttachment.IsVisible = false;
        var footer = ComposerPanel(_composer, SendMessageAsync, _attachmentStatus,
            GlyphButton("Attach a file", AttachmentIcon, SelectAttachmentAsync),
            QuietButton("@ Mention", () => ShowMentionsAsync(channel)), _removeAttachment);
        _timeline = ShowConversation(header, _messages, footer, _typingStatus);
        await RenderMessagesAsync();
        ScrollToLatest(_timeline);
        UpdateVoice();
    }

    private async Task CloseChannelViewAsync()
    {
        await StopTypingAsync();
        _channel = null;
        _attachment?.Dispose();
        _attachment = null;
        _typing.Clear();
        _displayCache.Clear();
        _messages = null;
        _timeline = null;
        _composer = null;
        _voiceStatus = null;
        _voiceAction = null;
        _muteAction = null;
        _messageFilter = null;
        _typingStatus = null;
        _attachmentStatus = null;
        _removeAttachment = null;
    }

    private async Task RenderMessagesSafelyAsync()
    {
        try { await RenderMessagesAsync(); }
        catch (Exception ex) { Notify(ex.Message); }
    }

    private async Task RenderMessagesAsync()
    {
        if (_channel is not { } channel || _messages is not { } panel || _session.User is not { } user)
            return;
        var query = _messageFilter?.Text?.Trim();
        var history = await _session.ReadHistoryAsync(channel.Id, string.IsNullOrWhiteSpace(query) ? _messageLimit : null);
        var controls = new List<Control>();
        DateOnly? previousDay = null;
        foreach (var message in history)
        {
            var cipher = message.Content ?? string.Empty;
            if (!_displayCache.TryGetValue(message.ReferenceId, out var display) || display.Cipher != cipher)
            {
                display = (cipher, await _session.Encryption.GetDisplayContentAsync(message) ?? string.Empty);
                if (channel == _channel)
                    _displayCache[message.ReferenceId] = display;
            }
            var plain = display.Text;
            if (!string.IsNullOrWhiteSpace(query) && !plain.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            if ((message.DateSent ?? message.DateCreated) is { } timestamp)
            {
                var day = DateOnly.FromDateTime(timestamp.LocalDateTime);
                if (day != previousDay)
                    controls.Add(TimelineDate(day));
                previousDay = day;
            }
            controls.Add(CreateMessageBubble(message, plain, user.Id));
        }
        if (channel != _channel || panel != _messages || query != _messageFilter?.Text?.Trim())
            return;
        panel.Children.Clear();
        foreach (var control in controls)
            panel.Children.Add(control);
        if (controls.Count == 0)
            panel.Children.Add(string.IsNullOrWhiteSpace(query)
                ? EmptyState(ChatIcon, "Start the conversation", "Write a message below to get things going.")
                : EmptyState(SearchIcon, "No matching messages", "Try a different search term."));
    }

    private Border CreateMessageBubble(Message message, string text, Guid currentUser)
    {
        var agent = message.AgentDefinitionId.HasValue;
        var sender = message.SenderId == currentUser && !agent;
        var name = sender ? "You" : message.DisplayName ?? (agent ? "Agent" : "User");
        var time = (message.DateSent ?? message.DateCreated)?.ToLocalTime();
        var body = new StackPanel { Spacing = 6 };
        var metadata = Actions(Text(name, 13, Ink, FontWeight.SemiBold));
        metadata.ItemSpacing = 6;
        metadata.LineSpacing = 4;
        if (agent)
        {
            metadata.Children.Add(new Border { Background = TokenSurface, CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5, 1), Child = Text("Agent", 11, AccentInk, FontWeight.SemiBold) });
        }
        if (time is { } sent)
        {
            var timestamp = Text(sent.ToString("t"), 11, Muted);
            ToolTip.SetTip(timestamp, sent.ToString("g"));
            metadata.Children.Add(timestamp);
        }
        if (sender && time is not null)
            metadata.Children.Add(Text(message.DateSeen.HasValue ? "Seen" : "Sent", 11, AccentInk));
        body.Children.Add(metadata);
        body.Children.Add(RenderMessageBody(text, Ink));
        foreach (Match match in MessageTokens.Matches(text))
        {
            if (!match.Groups["file"].Success)
                continue;
            var token = match.Groups["file"].Value;
            var descriptor = EncryptedFileTokenParser.Parse(token);
            if (!descriptor.IsImage)
                continue;
            var preview = new Image { MaxHeight = 280, Stretch = Stretch.Uniform, IsVisible = false };
            preview.DetachedFromVisualTree += (_, _) => (preview.Source as Bitmap)?.Dispose();
            body.Children.Add(MessageAction($"Preview {descriptor.Label}", async () =>
            {
                if (preview.Source is null)
                {
                    var file = await _session.DownloadAsync(token);
                    using var stream = new MemoryStream(file.Bytes);
                    preview.Source = Bitmap.DecodeToWidth(stream, 640);
                }
                preview.IsVisible = !preview.IsVisible;
            }));
            body.Children.Add(preview);
        }
        var bubble = new Border
        {
            Background = agent ? AgentSurface : sender ? SenderSurface : Surface,
            BorderBrush = agent ? AgentLine : sender ? SenderLine : Line,
            BorderThickness = new Thickness(1), MaxWidth = 650,
            HorizontalAlignment = sender ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            CornerRadius = sender ? new CornerRadius(13, 13, 4, 13) : new CornerRadius(4, 13, 13, 13),
            Padding = new Thickness(12, 9), Child = body
        };
        bubble.Classes.Add(sender ? "sender-message" : agent ? "agent-message" : "receiver-message");
        if (sender)
            return bubble;

        var avatar = agent ? BadgeIcon(AgentIcon, TokenSurface, AccentInk, 30) : Avatar(name, 30);
        avatar.VerticalAlignment = VerticalAlignment.Top;
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        row.Children.Add(avatar);
        Grid.SetColumn(bubble, 1);
        row.Children.Add(bubble);
        return new Border { Child = row };
    }

    private TextBlock RenderMessage(string text, IBrush foreground, bool includeChannelTokens = true)
    {
        var block = new TextBlock { Foreground = foreground, TextWrapping = TextWrapping.Wrap };
        var position = 0;
        foreach (Match token in MessageTokens.Matches(text))
        {
            block.Inlines!.Add(new Run(text[position..token.Index]));
            position = token.Index + token.Length;
            if (!includeChannelTokens && (token.Groups["mention"].Success || token.Groups["file"].Success))
            {
                block.Inlines.Add(new Run(token.Value));
                continue;
            }
            if (token.Groups["mention"].Success)
            {
                block.Inlines.Add(new InlineUIContainer(new Border
                {
                    Background = TokenSurface, CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(5, 1), Child = new TextBlock
                    {
                        Text = "@" + token.Groups["mention"].Value.Trim('<', '>'), Foreground = AccentInk,
                        FontWeight = FontWeight.SemiBold, MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }) { BaselineAlignment = BaselineAlignment.Center });
            }
            else if (token.Groups["file"].Success)
            {
                var fileToken = token.Groups["file"].Value;
                block.Inlines.Add(new InlineUIContainer(MessageAction(EncryptedFileTokenParser.Parse(fileToken).Label,
                    () => DownloadAsync(fileToken))) { BaselineAlignment = BaselineAlignment.Center });
            }
            else
            {
                if (!Uri.TryCreate(token.Groups["link"].Success ? token.Groups["link"].Value : token.Value, UriKind.Absolute, out var uri))
                {
                    block.Inlines.Add(new Run(token.Value));
                    continue;
                }
                var label = token.Groups["label"].Success ? token.Groups["label"].Value : token.Value;
                block.Inlines.Add(new InlineUIContainer(MessageAction(label, async () =>
                {
                    if (TopLevel.GetTopLevel(this) is { } top)
                        await top.Launcher.LaunchUriAsync(uri);
                })) { BaselineAlignment = BaselineAlignment.Center });
            }
        }
        block.Inlines!.Add(new Run(text[position..]));
        return block;
    }

    private Button MessageAction(string label, Func<Task> action)
    {
        var button = StyledButton(label, action, "message-action");
        var text = Text(label, 13, AccentInk, FontWeight.SemiBold);
        text.TextDecorations = TextDecorations.Underline;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.TextWrapping = TextWrapping.NoWrap;
        text.MaxWidth = 188;
        button.Content = text;
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        return button;
    }

    private async Task ReceiveAsync(Message message, bool updateOnly = false)
    {
        try
        {
            var activeChannel = _channel?.Id == message.ChannelId;
            var atEnd = _timeline is null || _timeline.Offset.Y >= _timeline.Extent.Height - _timeline.Viewport.Height - 48;
            await _session.StoreMessageAsync(message, updateOnly);
            if (!updateOnly && _session.User is { } user && message.Id != Guid.Empty && message.Content is not null)
            {
                if (activeChannel && message.SenderId != user.Id && message.DateSeen is null)
                    await _session.Messages.UpdateMessage(message);
                await _session.Messages.UpdatePendingMessage(message.Id);
            }
            if (_channel?.Id != message.ChannelId)
                return;

            _typing.Remove(ParticipantKey(message.SenderId, message.AgentDefinitionId));
            UpdateTypingStatus();
            await RenderMessagesAsync();
            if (atEnd)
                ScrollToLatest(_timeline);
        }
        catch (Exception ex) { Notify(ex.Message); }
    }

    private async Task SendMessageAsync()
    {
        if (_channel is not { } channel || _session.User is not { } user || _composer is not { } composer)
            return;
        var text = composer.Text?.Trim() ?? string.Empty;
        if (text.Length == 0 && _attachment is null)
            return;

        await StopTypingAsync();
        var profile = await _session.Users.GetUserPrivateDetails() ?? user;
        var readiness = await _session.Encryption.EnsureIdentityAsync(profile);
        if (!readiness.IsReady)
            throw new InvalidOperationException(readiness.Message);
        var recipients = (await _session.Channels.GetChannelUsers(channel.Id) ?? [])
            .Where(recipient => recipient.Id != user.Id)
            .Append(await _session.Users.GetUserPrivateDetails() ?? profile);
        var agents = await _session.Agents.GetChannelAgents(channel.Id);
        var agentText = text;
        if (_attachment is { } file)
        {
            var (summary, token) = await UploadAsync(channel, file);
            text = $"{text}\n\n{summary}\n{token}".Trim();
            agentText = $"{agentText}\n\n{summary}".Trim();
        }
        var payload = await _session.Encryption.EncryptMessageForRecipientsAsync(text, recipients, agents, agentText);
        var message = new Message
        {
            ChannelId = channel.Id, SenderId = user.Id, DisplayName = UserName(user),
            RecipientContents = payload.UserRecipientContents, AgentRecipientContents = payload.AgentRecipientContents
        };
        if (!await _session.Messages.SendMessage(message))
            throw new InvalidOperationException("The message could not be sent.");

        message.Content = payload.UserRecipientContents.Single(item => item.UserId == user.Id).Content;
        message.DateSent = DateTimeOffset.UtcNow;
        await _session.StoreMessageAsync(message);
        composer.Text = string.Empty;
        _attachment?.Dispose();
        _attachment = null;
        UpdateAttachmentStatus();
        await RenderMessagesAsync();
        ScrollToLatest(_timeline);
    }

    private async Task<(string Summary, string Token)> UploadAsync(Channel channel, IStorageFile file)
    {
        var bytes = await ReadFileAsync(file, MaxAttachmentBytes);
        var encrypted = await _session.Encryption.EncryptFileAsync(bytes, file.Name, FileContentType(file.Name));
        await using var cipher = new MemoryStream(encrypted.CipherBytes);
        var uploaded = await _session.Files.Upload(channel.Id, cipher, $"egroo-{Guid.NewGuid():N}.bin", "application/octet-stream")
            ?? throw new InvalidOperationException("The attachment could not be uploaded.");
        return ($"Attached file: {file.Name}", _session.Encryption.BuildEncryptedFileToken(
            new Uri(_session.Api, uploaded.Url).AbsoluteUri, encrypted));
    }

    private async Task SelectAttachmentAsync()
    {
        var file = await PickFileAsync("Attach a file");
        if (file is null)
            return;
        _attachment?.Dispose();
        _attachment = file;
        UpdateAttachmentStatus();
    }

    private void UpdateAttachmentStatus()
    {
        if (_attachmentStatus is not null)
        {
            _attachmentStatus.Text = _attachment?.Name;
            _attachmentStatus.IsVisible = _attachment is not null;
        }
        if (_removeAttachment is not null)
            _removeAttachment.IsVisible = _attachment is not null;
    }

    private async Task<IStorageFile?> PickFileAsync(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        return storage is null ? null : (await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title })).FirstOrDefault();
    }

    private static async Task<byte[]> ReadFileAsync(IStorageFile file, int limit)
    {
        await using var source = await file.OpenReadAsync();
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(buffer)) > 0)
        {
            if (output.Length + count > limit)
                throw new InvalidOperationException("Files are limited to 1 MB.");
            await output.WriteAsync(buffer.AsMemory(0, count));
        }
        return output.ToArray();
    }

    private async Task DownloadAsync(string token)
    {
        var file = await _session.DownloadAsync(token);
        if (OperatingSystem.IsBrowser())
        {
            await _runtime.InvokeVoidAsync("file.save", Convert.ToBase64String(file.Bytes), file.Name);
            return;
        }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage?.CanSave != true && _exportFile is not null)
        {
            await _exportFile(file.Name, file.Bytes);
            return;
        }
        using var target = storage is null ? null : await storage.SaveFilePickerAsync(
            new FilePickerSaveOptions { SuggestedFileName = file.Name });
        if (target is null)
            return;
        await using var stream = await target.OpenWriteAsync();
        if (stream.CanSeek)
            stream.SetLength(0);
        await stream.WriteAsync(file.Bytes);
    }

    private async Task ShowMembersAsync(Channel channel)
    {
        var members = await _session.Channels.GetChannelUsers(channel.Id) ?? [];
        var agentList = await _session.Agents.GetChannelAgents(channel.Id) ?? [];
        var admin = await _session.Channels.IsChannelAdmin(channel.Id, _session.User!.Id);
        var panel = Page("Channel members", ChannelTitle(channel), IconButton("Back to chat", BackIcon, () => OpenChannelAsync(channel)));
        var participants = SettingsGroup(panel, "People and agents");
        foreach (var member in members)
        {
            participants.Children.Add(admin && member.Id != _session.User.Id
                ? Actions(Label(UserName(member)), Button("Remove", async () =>
                {
                    await RequireAsync(_session.Channels.RemoveChannelUser(channel.Id, member.Id));
                    await ShowMembersAsync(channel);
                })) : Label(UserName(member)));
        }
        foreach (var agent in agentList)
        {
            participants.Children.Add(admin
                ? Actions(Label($"{agent.Name} · Agent"), Button("Remove", async () =>
                {
                    await RequireAsync(_session.Agents.RemoveAgentFromChannel(channel.Id, agent.Id));
                    await ShowMembersAsync(channel);
                })) : Label($"{agent.Name} · Agent"));
        }
        var invite = Field("Usernames separated by commas");
        var management = SettingsGroup(panel, "Add members");
        management.Children.Add(Actions(invite, PrimaryButton("Invite", async () =>
        {
            await RequireAsync(_session.Channels.AddChannelUsers(channel.Id,
                (invite.Text ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));
            await ShowMembersAsync(channel);
        })));
        if (admin)
        {
            var agents = (await _session.Agents.GetAgents() ?? []).Concat(await _session.Agents.SearchPublishedAgents() ?? [])
                .DistinctBy(agent => agent.Id);
            foreach (var agent in agents.Where(agent => agentList.All(item => item.Id != agent.Id)))
                management.Children.Add(Button($"Add {agent.Name}", async () =>
                {
                    await RequireAsync(_session.Agents.AddAgentToChannel(channel.Id, agent.Id));
                    await ShowMembersAsync(channel);
                }));
        }
        var options = SettingsGroup(panel, "Channel access");
        options.Children.Add(StyledButton("Leave channel", async () =>
        {
            await RequireAsync(_session.Channels.LeaveChannel(channel.Id));
            await ShowChannelsAsync();
        }, "danger"));
        if (admin)
            AddConfirmation(options, "Delete channel", async () =>
            {
                await RequireAsync(_session.Channels.DeleteChannel(channel.Id));
                await ShowChannelsAsync();
            });
    }

    private async Task ShowMentionsAsync(Channel channel)
    {
        if (_composer is not { } composer)
            return;
        var users = await _session.Channels.GetChannelUsers(channel.Id) ?? [];
        var agents = await _session.Agents.GetChannelAgents(channel.Id) ?? [];
        var menu = new ContextMenu();
        foreach (var name in users.Select(user => user.Username ?? UserName(user)).Concat(agents.Select(agent => agent.Name)))
        {
            var option = new MenuItem { Header = name };
            option.Click += (_, _) =>
            {
                composer.Text = $"{composer.Text} @<{name}> ".TrimStart();
                composer.CaretIndex = composer.Text.Length;
                composer.Focus();
            };
            menu.Items.Add(option);
        }
        menu.Open(composer);
    }

    private async Task ShowAudioSettingsAsync(Channel channel)
    {
        var panel = Page("Audio settings", ChannelTitle(channel), IconButton("Back to chat", BackIcon, () => OpenChannelAsync(channel)));
        var processing = SettingsGroup(panel, "Microphone processing");
        var voice = _session.Voice;
        foreach (var (name, label, enabled) in new[]
        {
            ("noiseSuppression", "Noise suppression", voice.IsNoiseSuppressionEnabled),
            ("echoCancellation", "Echo cancellation", voice.IsEchoCancellationEnabled),
            ("autoGainControl", "Automatic gain control", voice.IsAutoGainControlEnabled)
        })
        {
            var option = new CheckBox { Content = label, IsChecked = enabled };
            option.IsCheckedChanged += (_, _) => _ = RunAsync(() => voice.UpdateAudioProcessingSettingAsync(name, option.IsChecked == true, label));
            processing.Children.Add(option);
        }
        await Task.CompletedTask;
    }

    private void UpdateVoice()
    {
        var voice = _session.Voice;
        if (_voiceStatus is not null)
        {
            _voiceStatus.Text = voice.IsInCall ? $"{voice.CurrentChannelTitle} · {voice.CallDuration}{(voice.IsMuted ? " · Muted" : "")}" : null;
            _voiceStatus.IsVisible = voice.IsInCall;
        }
        if (_voiceAction is not null && _channel is { } channel)
            _voiceAction.Content = voice.IsCurrentChannel(channel.Id) ? "Leave voice" : "Join voice";
        if (_muteAction is not null)
        {
            _muteAction.IsVisible = voice.IsInCall;
            _muteAction.Content = voice.IsMuted ? "Unmute" : "Mute";
        }
    }

    private static string ParticipantKey(Guid user, Guid? agent) => agent?.ToString() ?? user.ToString();

    private void UpdateTyping(ChannelTypingState state, bool starts)
    {
        if (_channel?.Id != state.ChannelId || (!state.IsAgent && _session.User?.Id == state.UserId))
            return;
        var key = ParticipantKey(state.UserId, state.AgentDefinitionId);
        if (starts)
            _typing[key] = state;
        else
            _typing.Remove(key);
        UpdateTypingStatus();
    }

    private void UpdateTypingStatus()
    {
        if (_typingStatus is not null)
        {
            _typingStatus.Text = _typing.Count == 0 ? null
                : $"{string.Join(", ", _typing.Values.Select(state => state.DisplayName ?? (state.IsAgent ? "Agent" : "User")))} typing…";
            _typingStatus.IsVisible = _typing.Count != 0;
        }
    }

    private async Task UpdateComposerTypingAsync()
    {
        if (!_session.Connected || _channel is not { } channel || string.IsNullOrWhiteSpace(_composer?.Text))
            return;
        _typingTimer.Stop();
        _typingTimer.Start();
        if (_isTyping)
            return;
        _isTyping = true;
        try { await _session.Messages.StartTyping(channel.Id); }
        catch (Exception) { _isTyping = false; }
    }

    private async void TypingTimerElapsed(object? sender, EventArgs e) => await StopTypingAsync();

    private async Task StopTypingAsync()
    {
        _typingTimer.Stop();
        if (!_isTyping)
            return;
        _isTyping = false;
        try
        {
            if (_channel is { } channel)
                await _session.Messages.StopTyping(channel.Id);
        }
        catch (Exception) { }
    }
}
