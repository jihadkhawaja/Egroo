using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
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
    private readonly Dictionary<string, ChannelTypingState> _typing = new();
    private readonly Dictionary<Guid, (string Cipher, string Text)> _displayCache = new();
    private readonly DispatcherTimer _typingTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private StackPanel? _messages;
    private ScrollViewer? _timeline;
    private TextBlock? _typingStatus;
    private TextBlock? _voiceStatus;
    private TextBox? _composer;
    private TextBox? _messageFilter;
    private IStorageFile? _attachment;
    private TextBlock? _attachmentStatus;
    private bool _isTyping;
    private int _messageLimit;

    private async Task OpenChannelAsync(Channel channel)
    {
        await StopTypingAsync();
        _attachment?.Dispose();
        _attachment = null;
        _channel = channel;
        _typing.Clear();
        _displayCache.Clear();
        _messageLimit = 150;

        var channelGrid = new Grid
        {
            Margin = new Thickness(16, 0, 16, 6), RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };
        var header = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = ChannelTitle(channel), FontSize = 22, FontWeight = FontWeight.SemiBold });
        _voiceStatus = Label(null);
        _messageFilter = Field("Search messages");
        _messageFilter.TextChanged += (_, _) => _ = RenderMessagesSafelyAsync();
        header.Children.Add(Actions(Button("Members", () => ShowMembersAsync(channel)), _messageFilter,
            Button("Older messages", async () => { _messageLimit += 150; await RenderMessagesAsync(); }),
            Button("Join / leave voice", async () =>
            {
                if (_session.Voice.IsCurrentChannel(channel.Id))
                    await _session.Voice.LeaveCallAsync();
                else
                    await _session.Voice.JoinCallAsync(channel.Id, ChannelTitle(channel));
            }),
            Button("Mute / unmute", () => _session.Voice.ToggleMuteAsync()),
            Button("Audio settings", () => ShowAudioSettingsAsync(channel))));
        header.Children.Add(_voiceStatus);
        channelGrid.Children.Add(header);

        _messages = new StackPanel { Spacing = 8 };
        _timeline = new ScrollViewer { Content = _messages };
        Grid.SetRow(_timeline, 1);
        channelGrid.Children.Add(_timeline);

        _composer = Field("Write a message");
        _composer.AcceptsReturn = true;
        _composer.TextWrapping = TextWrapping.Wrap;
        _composer.MaxHeight = 120;
        _composer.TextChanged += (_, _) => _ = UpdateComposerTypingAsync();
        _composer.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && !args.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                args.Handled = true;
                _ = RunAsync(SendMessageAsync);
            }
        };
        _typingTimer.Stop();
        _typingTimer.Tick -= TypingTimerElapsed;
        _typingTimer.Tick += TypingTimerElapsed;
        _typingStatus = Label(null);
        _attachmentStatus = Label(null);
        var footer = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        footer.Children.Add(_typingStatus);
        footer.Children.Add(_attachmentStatus);
        footer.Children.Add(_composer);
        footer.Children.Add(Actions(Button("Send", SendMessageAsync), Button("Attach", SelectAttachmentAsync),
            Button("Remove attachment", () =>
            {
                _attachment?.Dispose();
                _attachment = null;
                _attachmentStatus.Text = null;
                return Task.CompletedTask;
            }), Button("@ Mention", () => ShowMentionsAsync(channel))));
        Grid.SetRow(footer, 2);
        channelGrid.Children.Add(footer);
        _body.Content = channelGrid;
        await RenderMessagesAsync();
        _timeline.ScrollToEnd();
        UpdateVoice();
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
            controls.Add(CreateMessageBubble(message, plain, user.Id));
        }
        if (channel != _channel || panel != _messages || query != _messageFilter?.Text?.Trim())
            return;
        panel.Children.Clear();
        foreach (var control in controls)
            panel.Children.Add(control);
        if (controls.Count == 0)
            panel.Children.Add(Label("No messages."));
    }

    private Border CreateMessageBubble(Message message, string text, Guid currentUser)
    {
        var agent = message.AgentDefinitionId.HasValue;
        var sender = message.SenderId == currentUser && !agent;
        var foreground = new SolidColorBrush(Color.Parse("#F8FAFF"));
        var timestamp = (message.DateSent ?? message.DateCreated)?.ToLocalTime().ToString("g");
        var state = sender ? message.DateSeen.HasValue ? " · Seen" : " · Sent" : "";
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = $"{(sender ? "You" : message.DisplayName ?? (agent ? "Agent" : "User"))}{(agent ? " · Agent" : "")}  {timestamp}{state}",
            Foreground = foreground, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(RenderMessageBody(text, foreground));
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
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(agent ? "#392B59" : sender ? "#174564" : "#273348")),
            HorizontalAlignment = sender ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(10), MaxWidth = 720, Padding = new Thickness(12, 9), Child = body
        };
    }

    private TextBlock RenderMessage(string text, IBrush foreground)
    {
        var block = new TextBlock { Foreground = foreground, TextWrapping = TextWrapping.Wrap };
        var position = 0;
        foreach (Match token in MessageTokens.Matches(text))
        {
            block.Inlines!.Add(new Run(text[position..token.Index]));
            position = token.Index + token.Length;
            if (token.Groups["mention"].Success)
            {
                block.Inlines.Add(new InlineUIContainer(new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#526891")), CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(5, 1), Child = new TextBlock
                    {
                        Text = "@" + token.Groups["mention"].Value.Trim('<', '>'), Foreground = foreground
                    }
                }));
            }
            else if (token.Groups["file"].Success)
            {
                var fileToken = token.Groups["file"].Value;
                block.Inlines.Add(new InlineUIContainer(MessageAction(EncryptedFileTokenParser.Parse(fileToken).Label,
                    () => DownloadAsync(fileToken))));
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
                })));
            }
        }
        block.Inlines!.Add(new Run(text[position..]));
        return block;
    }

    private Button MessageAction(string label, Func<Task> action)
    {
        var button = Button(label, action);
        button.Padding = new Thickness(5, 1);
        button.Foreground = new SolidColorBrush(Color.Parse("#C6E5FF"));
        button.Background = new SolidColorBrush(Color.Parse("#243953"));
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
                _timeline?.ScrollToEnd();
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
        if (_attachmentStatus is not null)
            _attachmentStatus.Text = null;
        await RenderMessagesAsync();
        _timeline?.ScrollToEnd();
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
        if (_attachmentStatus is not null)
            _attachmentStatus.Text = file.Name;
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
        var panel = Page("Channel members");
        panel.Children.Add(Button("Back to chat", () => OpenChannelAsync(channel)));
        foreach (var member in members)
        {
            panel.Children.Add(admin && member.Id != _session.User.Id
                ? Actions(Label(UserName(member)), Button("Remove", async () =>
                {
                    await RequireAsync(_session.Channels.RemoveChannelUser(channel.Id, member.Id));
                    await ShowMembersAsync(channel);
                })) : Label(UserName(member)));
        }
        foreach (var agent in agentList)
        {
            panel.Children.Add(admin
                ? Actions(Label($"{agent.Name} · Agent"), Button("Remove", async () =>
                {
                    await RequireAsync(_session.Agents.RemoveAgentFromChannel(channel.Id, agent.Id));
                    await ShowMembersAsync(channel);
                })) : Label($"{agent.Name} · Agent"));
        }
        var invite = Field("Usernames separated by commas");
        panel.Children.Add(Actions(invite, Button("Add members", async () =>
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
                panel.Children.Add(Button($"Add {agent.Name}", async () =>
                {
                    await RequireAsync(_session.Agents.AddAgentToChannel(channel.Id, agent.Id));
                    await ShowMembersAsync(channel);
                }));
        }
        panel.Children.Add(Button("Leave channel", async () =>
        {
            await RequireAsync(_session.Channels.LeaveChannel(channel.Id));
            await ShowChannelsAsync();
        }));
        if (admin)
            AddConfirmation(panel, "Delete channel", async () =>
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
        var panel = Page("Audio settings");
        panel.Children.Add(Button("Back to chat", () => OpenChannelAsync(channel)));
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
            panel.Children.Add(option);
        }
        await Task.CompletedTask;
    }

    private void UpdateVoice()
    {
        var voice = _session.Voice;
        if (_voiceStatus is not null)
            _voiceStatus.Text = voice.IsInCall ? $"{voice.CurrentChannelTitle} · {voice.CallDuration}{(voice.IsMuted ? " · Muted" : "")}" : null;
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
            _typingStatus.Text = _typing.Count == 0 ? null
                : $"{string.Join(", ", _typing.Values.Select(state => state.DisplayName ?? (state.IsAgent ? "Agent" : "User")))} typing…";
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
