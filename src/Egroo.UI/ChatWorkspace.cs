using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Egroo.Contracts.Models;
using Egroo.UI.Services;

namespace Egroo.UI;

public sealed partial class ChatWorkspace : UserControl
{
    private readonly ClientRuntime _runtime;
    private readonly WorkspaceSession _session;
    private readonly Func<string, byte[], Task>? _exportFile;
    private readonly Grid _root = new() { RowDefinitions = new RowDefinitions("*,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,*") };
    private readonly ContentControl _body = new()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch
    };
    private readonly TextBlock _status = Text(null, 13, AccentInk);
    private readonly DispatcherTimer _mediaTimer = new() { Interval = TimeSpan.FromMilliseconds(125) };
    private bool _receivingMedia;
    private bool _loading;
    private bool _started;
    private bool _wasConnected;
    private Channel? _channel;

    public ChatWorkspace(Uri api, ClientRuntime runtime, Func<string, byte[], Task>? exportFile = null,
        Func<ClientRuntime>? nearbyRuntime = null)
    {
        _runtime = runtime;
        _exportFile = exportFile;
        _nearby = new NearbySessionService(nearbyRuntime ?? (() => runtime));
        _session = new WorkspaceSession(api, runtime, Notify);
        _session.Received += message => Dispatcher.UIThread.Post(() => _ = ReceiveAsync(message));
        _session.Updated += message => Dispatcher.UIThread.Post(() => _ = ReceiveAsync(message, updateOnly: true));
        _session.Typing += (state, starts) => Dispatcher.UIThread.Post(() => UpdateTyping(state, starts));
        _session.ConnectionChanged += () => Dispatcher.UIThread.Post(() => _ = ConnectionChangedAsync());
        _session.Voice.StateChanged += () => Dispatcher.UIThread.Post(UpdateVoice);

        BuildShell();
        ShowAuthentication();
        _mediaTimer.Tick += (_, _) => _ = PollMediaAsync();
        _nearbyTimer.Tick += (_, _) => PollNearby();
        AttachedToVisualTree += (_, _) =>
        {
            _mediaTimer.Start();
            if (!_started)
            {
                _started = true;
                _ = RestoreAsync();
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _mediaTimer.Stop();
            _nearbyTimer.Stop();
        };
    }

    private async Task RestoreAsync()
    {
        await RunAsync(async () =>
        {
            await _session.RestoreAsync();
            if (_session.User is not null)
                await OpenWorkspaceAsync();
        });
    }

    private void ShowAuthentication(bool create = false, string? usernameValue = null)
    {
        SetWorkspaceOpen(false);
        var username = Field("", usernameValue);
        var password = Field("", secret: true);
        password.Classes.Add("revealPasswordButton");
        var form = new StackPanel { Spacing = 16 };
        form.Children.Add(Brand());
        var heading = Text(create ? "Create an account" : "Sign in", 31, Ink, FontWeight.Bold);
        heading.Margin = new Thickness(0, 7, 0, 3);
        form.Children.Add(heading);
        form.Children.Add(LabeledField("Username", username));
        if (create)
            form.Children.Add(Text("Use 3–16 letters, numbers, or underscores.", 12, Muted));
        form.Children.Add(LabeledField("Password", password));
        if (create)
            form.Children.Add(Text("Use at least 8 characters with uppercase and lowercase letters, a number, and a symbol.", 12, Muted));
        async Task AuthenticateAsync()
        {
            if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(password.Text))
                throw new InvalidOperationException("Enter a username and password.");
            await _session.SignInAsync(username.Text.Trim(), password.Text, create);
            password.Text = null;
            await OpenWorkspaceAsync();
        }
        var submit = PrimaryButton(create ? "Create account" : "Sign in", AuthenticateAsync);
        submit.HorizontalAlignment = HorizontalAlignment.Stretch;
        form.Children.Add(submit);
        var switchMode = QuietButton(create ? "Already have an account? Sign in" : "New to Egroo? Create account", () =>
        {
            ShowAuthentication(!create, username.Text);
            return Task.CompletedTask;
        });
        switchMode.HorizontalAlignment = HorizontalAlignment.Center;
        form.Children.Add(switchMode);
        var nearby = StyledButton("Nearby chat without an account", ShowNearbyAsync, "quiet", serialize: false);
        nearby.HorizontalAlignment = HorizontalAlignment.Center;
        form.Children.Add(nearby);
        SubmitOnEnter(password, AuthenticateAsync);
        var authCard = Card(form, 28);
        authCard.MaxWidth = 430;
        authCard.VerticalAlignment = VerticalAlignment.Center;
        authCard.Margin = new Thickness(18, 24);
        authCard.BoxShadow = BoxShadows.Parse("0 14 42 0 #19000000");
        var backdrop = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(Color.Parse("#EFEFEF"), 0), new GradientStop(Color.Parse("#FAFAFA"), .55),
                new GradientStop(Color.Parse("#EAEAEA"), 1) }
        };
        _body.Content = new ScrollViewer { Content = authCard, Background = backdrop, VerticalContentAlignment = VerticalAlignment.Center };
    }

    private async Task OpenWorkspaceAsync()
    {
        _wasConnected = true;
        if (_nearbyOpen)
            return;
        SetWorkspaceOpen(true);
        await ShowChannelsAsync();
        await _session.Messages.SendPendingMessages();
    }

    private async Task SignOutAsync()
    {
        await CloseChannelViewAsync();
        await _session.SignOutAsync();
        _wasConnected = false;
        ShowAuthentication();
    }

    private async Task ConnectionChangedAsync()
    {
        if (_session.User is null)
            return;
        UpdateConnectionIndicator();
        if (!_session.Connected)
        {
            _wasConnected = false;
            Notify("Connection interrupted. Use Reconnect if the server stays offline.");
            return;
        }
        if (!_wasConnected)
        {
            _wasConnected = true;
            Notify(string.Empty);
            try
            {
                await _session.Messages.SendPendingMessages();
            }
            catch (Exception ex)
            {
                Notify(ex.Message);
            }
        }
    }

    private async Task PollMediaAsync()
    {
        if (_receivingMedia || !_session.Voice.IsInCall)
            return;
        _receivingMedia = true;
        try
        {
            await _runtime.DispatchMediaEventsAsync();
        }
        catch (Exception ex)
        {
            Notify(ex.Message);
        }
        finally
        {
            _receivingMedia = false;
        }
    }

    private Task ShowChannelsAsync() => ShowChannelsAsync(discover: false);

    private async Task ShowChannelsAsync(bool discover, string? search = null)
    {
        await CloseChannelViewAsync();
        SelectSection(WorkspaceSection.Channels);
        var members = Field("e.g. alex, sam");
        var creation = new StackPanel { Spacing = 12 };
        var createCard = Card(creation);
        createCard.IsVisible = false;

        Task RevealCreateAsync()
        {
            createCard.IsVisible = true;
            Dispatcher.UIThread.Post(() => members.Focus(), DispatcherPriority.Loaded);
            return Task.CompletedTask;
        }

        async Task CreateChannelAsync()
        {
            var usernames = (members.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (usernames.Length == 0 && _session.User?.Username is { } username)
                usernames = [username];
            var channel = await _session.Channels.CreateChannel(usernames);
            if (channel is null)
                throw new InvalidOperationException("Could not create the channel.");
            await OpenChannelAsync(channel);
        }

        var page = Page("Channels", "Start a conversation or find a public channel.",
            IconButton("New channel", PlusIcon, RevealCreateAsync, "primary"));
        creation.Children.Add(Text("New channel", 18, Ink, FontWeight.SemiBold));
        creation.Children.Add(LabeledField("Invite by username (optional)", members));
        creation.Children.Add(Actions(PrimaryButton("Create channel", CreateChannelAsync), QuietButton("Cancel", () =>
        {
            createCard.IsVisible = false;
            return Task.CompletedTask;
        })));
        SubmitOnEnter(members, CreateChannelAsync);
        page.Children.Add(createCard);

        var query = Field(discover ? "Search public channels" : "Filter your channels", search);
        AutomationProperties.SetName(query, query.PlaceholderText);
        var tabs = Actions(StyledButton("My channels", ShowChannelsAsync, discover ? "quiet" : "selected"),
            StyledButton("Discover", () => ShowChannelsAsync(true), discover ? "selected" : "quiet"));
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        searchRow.Children.Add(query);
        if (discover)
        {
            Task SearchAsync() => ShowChannelsAsync(true, query.Text?.Trim());
            var searchButton = IconButton("Search", SearchIcon, SearchAsync);
            Grid.SetColumn(searchButton, 1);
            searchRow.Children.Add(searchButton);
            SubmitOnEnter(query, SearchAsync);
        }
        var list = new StackPanel { Spacing = 8, Children = { Text("Loading channels…", 14, Muted) } };
        page.Children.Add(Card(new StackPanel
        {
            Spacing = 14, Children = { tabs, searchRow, new Border { Height = 1, Background = Line }, list }
        }));

        var loaded = discover
            ? await _session.Channels.SearchPublicChannels(search ?? string.Empty)
            : await _session.Channels.GetUserChannels();
        if (loaded is null)
        {
            list.Children.Clear();
            list.Children.Add(EmptyState(ChatIcon, "Couldn’t load channels", "Check your connection and try again.",
                Button("Try again", () => ShowChannelsAsync(discover, search))));
            return;
        }

        RenderChannelList();
        if (!discover)
            query.TextChanged += (_, _) => RenderChannelList();

        void RenderChannelList()
        {
            var filter = discover ? null : query.Text?.Trim();
            var channels = string.IsNullOrWhiteSpace(filter) ? loaded : loaded.Where(channel =>
                ChannelTitle(channel).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
            list.Children.Clear();
            foreach (var channel in channels)
            {
                var title = ChannelTitle(channel);
                var row = Button(title, async () =>
                {
                    if (discover && _session.User is { Username: { } username } user
                        && !await _session.Channels.ChannelContainUser(channel.Id, user.Id))
                        await RequireAsync(_session.Channels.AddChannelUsers(channel.Id, username));
                    await OpenChannelAsync(channel);
                });
                row.HorizontalAlignment = HorizontalAlignment.Stretch;
                row.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                var entry = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
                entry.Children.Add(RecordTitle(BadgeIcon(channel.IsPublic ? ChatIcon : LockIcon), title,
                    channel.IsPublic ? "Public channel" : "Private channel"));
                var chevron = Icon(ForwardIcon, Muted, 14);
                Grid.SetColumn(chevron, 1);
                entry.Children.Add(chevron);
                row.Content = entry;
                AutomationProperties.SetName(row, title);
                list.Children.Add(row);
            }

            if (channels.Length != 0)
                return;
            if (loaded.Length != 0 || !string.IsNullOrWhiteSpace(search))
                list.Children.Add(EmptyState(SearchIcon, "No matching channels", "Try a different search term."));
            else if (discover)
                list.Children.Add(EmptyState(ChatIcon, "No public channels yet", "Public channels will appear here when they’re available."));
            else
                list.Children.Add(EmptyState(ChatIcon, "No channels yet", "Create a channel to start chatting, or browse public channels to join a conversation.",
                    PrimaryButton("Create a channel", RevealCreateAsync), QuietButton("Browse public channels", () => ShowChannelsAsync(true))));
        }
    }

    private Button Button(string label, Func<Task> action, bool serialize = true)
    {
        var button = new Button { Content = label, Classes = { "workspace-action" } };
        button.Click += async (_, _) =>
        {
            if (serialize)
                await RunAsync(action);
            else
                await action();
        };
        return button;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_loading)
            return;
        _loading = true;
        try
        {
            Notify(string.Empty);
            await action();
        }
        catch (Exception ex)
        {
            Notify(ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private void Notify(string text) => SetNotice(text, nearby: false);

    private void SetNotice(string text, bool nearby)
    {
        if (Dispatcher.UIThread.CheckAccess())
            SetNotice();
        else
            Dispatcher.UIThread.Post(SetNotice);

        void SetNotice()
        {
            if (_nearbyOpen != nearby)
                return;
            _status.Text = text;
            _notice.IsVisible = !string.IsNullOrWhiteSpace(text);
        }
    }

    private void SubmitOnEnter(TextBox input, Func<Task> submit) => input.KeyDown += (_, args) =>
    {
        if (args.Key == Key.Enter)
        {
            args.Handled = true;
            _ = RunAsync(submit);
        }
    };

    private static WrapPanel Actions(params Control[] controls)
    {
        var panel = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        foreach (var control in controls)
            panel.Children.Add(control);
        return panel;
    }

    private static TextBox Field(string watermark, string? value = null, bool secret = false) => new()
    {
        PlaceholderText = watermark, Text = value, MinWidth = 128, Classes = { "workspace-field" },
        PasswordChar = secret ? '●' : default
    };

    private static TextBlock Label(string? text) => Text(text, 14, Muted);
    private static string ChannelTitle(Channel channel) => channel.GetTitle() ?? "Channel";
    private static string UserName(UserDto user) => user.UserDetail?.DisplayName ?? user.Username ?? "User";
}
