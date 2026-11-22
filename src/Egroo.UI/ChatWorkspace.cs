using Avalonia;
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
    private readonly Grid _root = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
    private readonly WrapPanel _navigation = new() { ItemSpacing = 8, LineSpacing = 6 };
    private readonly ContentControl _body = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 4, 16, 10) };
    private readonly DispatcherTimer _mediaTimer = new() { Interval = TimeSpan.FromMilliseconds(125) };
    private bool _receivingMedia;
    private bool _loading;
    private bool _started;
    private bool _wasConnected;
    private Channel? _channel;

    public ChatWorkspace(Uri api, ClientRuntime runtime, Func<string, byte[], Task>? exportFile = null)
    {
        _runtime = runtime;
        _exportFile = exportFile;
        _session = new WorkspaceSession(api, runtime, Notify);
        _session.Received += message => Dispatcher.UIThread.Post(() => _ = ReceiveAsync(message));
        _session.Updated += message => Dispatcher.UIThread.Post(() => _ = ReceiveAsync(message, updateOnly: true));
        _session.Typing += (state, starts) => Dispatcher.UIThread.Post(() => UpdateTyping(state, starts));
        _session.ConnectionChanged += () => Dispatcher.UIThread.Post(() => _ = ConnectionChangedAsync());
        _session.Voice.StateChanged += () => Dispatcher.UIThread.Post(UpdateVoice);

        _navigation.Margin = new Thickness(16, 12);
        _root.Children.Add(_navigation);
        Grid.SetRow(_body, 1);
        _root.Children.Add(_body);
        Grid.SetRow(_status, 2);
        _root.Children.Add(_status);
        Content = _root;
        ShowAuthentication();
        _mediaTimer.Tick += (_, _) => _ = PollMediaAsync();
        AttachedToVisualTree += (_, _) =>
        {
            _mediaTimer.Start();
            if (!_started)
            {
                _started = true;
                _ = RestoreAsync();
            }
        };
        DetachedFromVisualTree += (_, _) => _mediaTimer.Stop();
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

    private void ShowAuthentication()
    {
        _navigation.Children.Clear();
        var username = Field("Username");
        var password = Field("Password", secret: true);
        var form = new StackPanel
        {
            Spacing = 12, Margin = new Thickness(24), MaxWidth = 360,
            VerticalAlignment = VerticalAlignment.Center
        };
        form.Children.Add(new TextBlock { Text = "Egroo", FontSize = 38, FontWeight = FontWeight.SemiBold });
        form.Children.Add(username);
        form.Children.Add(password);
        async Task AuthenticateAsync(bool create)
        {
            if (string.IsNullOrWhiteSpace(username.Text) || string.IsNullOrWhiteSpace(password.Text))
                throw new InvalidOperationException("Enter a username and password.");
            await _session.SignInAsync(username.Text.Trim(), password.Text, create);
            password.Text = null;
            await OpenWorkspaceAsync();
        }
        form.Children.Add(Actions(Button("Sign in", () => AuthenticateAsync(false)),
            Button("Create account", () => AuthenticateAsync(true))));
        password.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
                _ = RunAsync(() => AuthenticateAsync(false));
        };
        _body.Content = new ScrollViewer { Content = form };
    }

    private async Task OpenWorkspaceAsync()
    {
        _wasConnected = true;
        _navigation.Children.Clear();
        _navigation.Children.Add(Button("Channels", ShowChannelsAsync));
        _navigation.Children.Add(Button("Friends", ShowFriendsAsync));
        _navigation.Children.Add(Button("Agents", ShowAgentsAsync));
        _navigation.Children.Add(Button("Settings", ShowSettingsAsync));
        _navigation.Children.Add(Button("Sign out", SignOutAsync));
        await ShowChannelsAsync();
        await _session.Messages.SendPendingMessages();
    }

    private async Task SignOutAsync()
    {
        await _session.SignOutAsync();
        _channel = null;
        _wasConnected = false;
        ShowAuthentication();
    }

    private async Task ConnectionChangedAsync()
    {
        if (_session.User is null)
            return;
        if (!_session.Connected)
        {
            _wasConnected = false;
            Notify("Connection interrupted. Use Reconnect if the server stays offline.");
            if (!_navigation.Children.Any(item => item is Button { Content: "Reconnect" }))
                _navigation.Children.Add(Button("Reconnect", RestoreAsync, serialize: false));
            return;
        }
        if (!_wasConnected)
        {
            _wasConnected = true;
            foreach (var item in _navigation.Children.Where(item => item is Button { Content: "Reconnect" }).ToArray())
                _navigation.Children.Remove(item);
            Notify("Connected");
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

    private Task ShowChannelsAsync() => ShowChannelsAsync(null);

    private async Task ShowChannelsAsync(string? search)
    {
        await StopTypingAsync();
        _channel = null;
        var panel = Page("Channels");
        var query = Field("Search public channels");
        var members = Field("Usernames separated by commas");
        panel.Children.Add(Actions(query, Button("Search", () => ShowChannelsAsync(query.Text)),
            Button("My channels", ShowChannelsAsync)));
        panel.Children.Add(Actions(members, Button("Create channel", async () =>
        {
            var usernames = (members.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (usernames.Length == 0 && _session.User?.Username is { } username)
                usernames = [username];
            var channel = await _session.Channels.CreateChannel(usernames);
            if (channel is null)
                throw new InvalidOperationException("Could not create the channel.");
            await OpenChannelAsync(channel);
        })));
        var channels = string.IsNullOrWhiteSpace(search)
            ? await _session.Channels.GetUserChannels()
            : await _session.Channels.SearchPublicChannels(search);
        foreach (var channel in channels ?? [])
            panel.Children.Add(Button(ChannelTitle(channel), async () =>
            {
                if (!string.IsNullOrWhiteSpace(search) && _session.User is { Username: { } username } user
                    && !await _session.Channels.ChannelContainUser(channel.Id, user.Id))
                    await RequireAsync(_session.Channels.AddChannelUsers(channel.Id, username));
                await OpenChannelAsync(channel);
            }));
        if (channels is null or { Length: 0 })
            panel.Children.Add(Label("No channels found."));
    }

    private StackPanel Page(string title)
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(16), MaxWidth = 960 };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeight.SemiBold });
        _body.Content = new ScrollViewer { Content = panel };
        return panel;
    }

    private Button Button(string label, Func<Task> action, bool serialize = true)
    {
        var button = new Button { Content = label };
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
            _status.Text = string.Empty;
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

    private void Notify(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _status.Text = text;
        else
            Dispatcher.UIThread.Post(() => _status.Text = text);
    }

    private static WrapPanel Actions(params Control[] controls)
    {
        var panel = new WrapPanel { ItemSpacing = 7, LineSpacing = 6 };
        foreach (var control in controls)
            panel.Children.Add(control);
        return panel;
    }

    private static TextBox Field(string watermark, string? value = null, bool secret = false) => new()
    {
        PlaceholderText = watermark, Text = value, MinWidth = 150,
        PasswordChar = secret ? '●' : default
    };

    private static TextBlock Label(string? text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private static string ChannelTitle(Channel channel) => channel.GetTitle() ?? "Channel";
    private static string UserName(UserDto user) => user.UserDetail?.DisplayName ?? user.Username ?? "User";
}
