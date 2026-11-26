using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using IconPath = Avalonia.Controls.Shapes.Path;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private enum WorkspaceSection { Channels, Friends, Agents, Settings }

    private const double CompactWidth = 720;
    private const string ChatIcon = "M4,5 H20 Q21,5 21,6 V16 Q21,17 20,17 H10 L5,21 V17 H4 Q3,17 3,16 V6 Q3,5 4,5 M7,10 H17 M7,13 H14";
    private const string FriendsIcon = "M12,11 A3.5,3.5 0 1 0 12,4 A3.5,3.5 0 0 0 12,11 M5,20 V18 Q5,14 9,14 H15 Q19,14 19,18 V20 M3,12 A2.5,2.5 0 0 1 5,8 M21,12 A2.5,2.5 0 0 0 19,8";
    private const string AgentIcon = "M12,3 V6 M6,7 H18 Q20,7 20,9 V18 Q20,20 18,20 H6 Q4,20 4,18 V9 Q4,7 6,7 M8,12 H9 M15,12 H16 M9,16 H15 M2,11 V16 M22,11 V16";
    private const string SettingsIcon = "M4,6 H20 M4,12 H20 M4,18 H20 M8,4 V8 M15,10 V14 M10,16 V20";
    private const string PlusIcon = "M12,5 V19 M5,12 H19";
    private const string BackIcon = "M15,5 L8,12 L15,19";
    private const string ForwardIcon = "M9,5 L16,12 L9,19";
    private const string SignOutIcon = "M12,5 H5 V19 H12 M10,12 H21 M17,8 L21,12 L17,16";
    private const string SearchIcon = "M16,10 A6,6 0 1 1 4,10 A6,6 0 0 1 16,10 M14.5,14.5 L20,20";
    private const string AttachmentIcon = "M9,14 L15,8 A2,2 0 0 1 18,11 L10,19 A4,4 0 0 1 4,13 L12,5";
    private const string SendIcon = "M3,4 L21,12 L3,20 L7,12 Z M7,12 H20";
    private const string LockIcon = "M7,11 V8 A5,5 0 0 1 17,8 V11 M6,11 H18 Q19,11 19,12 V20 H5 V12 Q5,11 6,11 M12,15 V17";

    private static readonly IBrush Canvas = Brush("#F7F7F7");
    private static readonly IBrush Surface = Brushes.White;
    private static readonly IBrush Ink = Brush("#191919");
    private static readonly IBrush Muted = Brush("#555555");
    private static readonly IBrush Line = Brush("#E2E2E2");
    private static readonly IBrush Accent = Brush("#303030");
    private static readonly IBrush AccentInk = Brush("#252525");
    private static readonly IBrush AccentSoft = Brush("#EAEAEA");
    private static readonly IBrush SidebarInk = Brush("#E3E3E3");
    private static readonly IBrush SidebarSoft = Brush("#B6B6B6");

    private readonly Border _sidebar = new() { Width = 222 };
    private readonly Border _mobileBar = new();
    private readonly Border _titleBar = new();
    private readonly Border _notice = new();
    private readonly Grid _profile = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
    private readonly TextBlock _sectionName = Text("Channels", 14, Ink, FontWeight.SemiBold);
    private readonly TextBlock _connectionText = Text("Connected", 12, Muted, FontWeight.Medium);
    private readonly Border _connectionDot = new() { Width = 7, Height = 7, CornerRadius = new CornerRadius(4),
        BorderBrush = Muted, BorderThickness = new Thickness(1) };
    private readonly Dictionary<WorkspaceSection, (Button Desktop, Button Mobile)> _destinations = [];
    private Button? _reconnect;
    private bool _workspaceOpen;

    private void BuildShell()
    {
        FontFamily = new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter");
        FontSize = 14;
        Foreground = Ink;
        Styles.Add(new StyleInclude(new Uri("avares://Egroo.UI/"))
        {
            Source = new Uri("avares://Egroo.UI/Styles/Workspace.axaml")
        });

        var sidebarContent = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 28 };
        sidebarContent.Children.Add(Brand(onDarkSurface: true));
        var navigation = new StackPanel { Spacing = 5 };
        Grid.SetRow(navigation, 1);
        sidebarContent.Children.Add(navigation);
        Grid.SetRow(_profile, 2);
        sidebarContent.Children.Add(_profile);
        _sidebar.Background = Brush("#222222");
        _sidebar.Padding = new Thickness(14, 23, 14, 18);
        _sidebar.Child = sidebarContent;
        _root.Children.Add(_sidebar);

        var mobileNavigation = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 4 };
        _mobileBar.Background = Surface;
        _mobileBar.BorderBrush = Line;
        _mobileBar.BorderThickness = new Thickness(0, 1, 0, 0);
        _mobileBar.Padding = new Thickness(8, 5);
        _mobileBar.Child = mobileNavigation;
        Grid.SetRow(_mobileBar, 1);
        Grid.SetColumnSpan(_mobileBar, 2);
        _root.Children.Add(_mobileBar);

        AddDestination(WorkspaceSection.Channels, ChatIcon, ShowChannelsAsync);
        AddDestination(WorkspaceSection.Friends, FriendsIcon, ShowFriendsAsync);
        AddDestination(WorkspaceSection.Agents, AgentIcon, ShowAgentsAsync);
        AddDestination(WorkspaceSection.Settings, SettingsIcon, ShowSettingsAsync);

        var breadcrumb = Inline(Text("Egroo", 13, Muted, FontWeight.Medium), Icon(ForwardIcon, Muted, 11), _sectionName);
        _reconnect = StyledButton("Reconnect", ReconnectAsync, "quiet", serialize: false);
        _reconnect.IsVisible = false;
        var connection = Inline(_connectionDot, _connectionText, _reconnect);
        connection.HorizontalAlignment = HorizontalAlignment.Right;
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        bar.Children.Add(breadcrumb);
        Grid.SetColumn(connection, 1);
        bar.Children.Add(connection);
        _titleBar.Background = Surface;
        _titleBar.BorderBrush = Line;
        _titleBar.BorderThickness = new Thickness(0, 0, 0, 1);
        _titleBar.Child = bar;

        _notice.Background = AccentSoft;
        _notice.Padding = new Thickness(16, 10);
        _notice.Child = _status;
        _notice.IsVisible = false;
        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        main.Children.Add(_titleBar);
        Grid.SetRow(_body, 1);
        main.Children.Add(_body);
        Grid.SetRow(_publicProfileView, 1);
        main.Children.Add(_publicProfileView);
        Grid.SetRow(_notice, 2);
        main.Children.Add(_notice);
        Grid.SetColumn(main, 1);
        _root.Children.Add(main);
        _root.Background = Canvas;
        Content = _root;
        SizeChanged += (_, _) => UpdateShellLayout();
        UpdateShellLayout();

        void AddDestination(WorkspaceSection destination, string glyph, Func<Task> open)
        {
            var desktop = IconButton(destination.ToString(), glyph, open, "sidebar-action");
            desktop.HorizontalAlignment = HorizontalAlignment.Stretch;
            navigation.Children.Add(desktop);
            var mobile = StyledButton(destination.ToString(), open, "quiet", "mobile-action");
            mobile.Content = new StackPanel
            {
                Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center,
                Children = { Icon(glyph, Accent, 16), Text(destination.ToString(), 11, AccentInk, FontWeight.SemiBold) }
            };
            AutomationProperties.SetName(mobile, destination.ToString());
            Grid.SetColumn(mobile, _destinations.Count);
            mobileNavigation.Children.Add(mobile);
            _destinations.Add(destination, (desktop, mobile));
        }
    }

    private void UpdateShellLayout()
    {
        var compact = Bounds.Width < CompactWidth;
        Classes.Set("compact", compact);
        _sidebar.IsVisible = _workspaceOpen && !compact;
        _mobileBar.IsVisible = _workspaceOpen && compact;
        _titleBar.IsVisible = _workspaceOpen;
        _titleBar.Padding = compact ? new Thickness(14, 10) : new Thickness(32, 15);
    }

    private void SetWorkspaceOpen(bool open)
    {
        ClosePublicProfile();
        _workspaceOpen = open;
        _profile.Children.Clear();
        if (open && _session.User is { } user)
        {
            _profile.Children.Add(Avatar(UserName(user), 32));
            var name = Text(UserName(user), 13, Brushes.White, FontWeight.SemiBold);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            var handle = Text(user.Username is null ? null : $"@{user.Username}", 11, SidebarSoft);
            handle.TextTrimming = TextTrimming.CharacterEllipsis;
            handle.TextWrapping = TextWrapping.NoWrap;
            var identity = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, handle } };
            Grid.SetColumn(identity, 1);
            _profile.Children.Add(identity);
            var signOut = GlyphButton("Sign out", SignOutIcon, SignOutAsync, "sidebar-action");
            Grid.SetColumn(signOut, 2);
            _profile.Children.Add(signOut);
        }
        UpdateShellLayout();
        UpdateConnectionIndicator();
    }

    private void SelectSection(WorkspaceSection section)
    {
        ClosePublicProfile();
        _sectionName.Text = section.ToString();
        foreach (var (destination, buttons) in _destinations)
        {
            buttons.Desktop.Classes.Set("selected", destination == section);
            buttons.Mobile.Classes.Set("selected", destination == section);
        }
    }

    private void UpdateConnectionIndicator()
    {
        var connected = _session.Connected;
        _connectionText.Text = connected ? "Connected" : "Offline";
        _connectionDot.Background = connected ? Muted : Brushes.Transparent;
        if (_reconnect is not null)
        {
            _reconnect.Content = connected ? "Retry messages" : "Reconnect";
            _reconnect.IsVisible = _workspaceOpen && (!connected || PendingReplayFailed);
        }
    }

    private StackPanel Page(string title, string? description = null, Button? action = null)
    {
        var panel = new StackPanel { Spacing = 18, MaxWidth = 1150 };
        panel.Classes.Add("workspace-page");
        panel.Children.Add(PageHeading(title, description, action));
        _body.Content = new ScrollViewer { Content = panel };
        return panel;
    }

    private static Grid PageHeading(string title, string? description, Button? action)
    {
        var heading = new TextBlock { Text = title, Foreground = Ink, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, Classes = { "page-title" } };
        var text = new StackPanel { Spacing = 6, Children = { heading } };
        if (description is not null)
            text.Children.Add(Text(description, 14, Muted));
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Children = { text } };
        if (action is not null)
        {
            action.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(action, 1);
            header.Children.Add(action);
        }
        return header;
    }

    private Border EmptyState(string glyph, string title, string detail, params Control[] actions)
    {
        var illustration = new Grid { Width = 104, Height = 96, HorizontalAlignment = HorizontalAlignment.Center };
        illustration.Children.Add(new Border
        {
            Width = 92, Height = 92, CornerRadius = new CornerRadius(46), Background = Brush("#F3F3F3"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        });
        illustration.Children.Add(new Border
        {
            Width = 57, Height = 57, CornerRadius = new CornerRadius(18), Background = AccentSoft,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = Icon(glyph, Accent, 28)
        });
        illustration.Children.Add(new Border
        {
            Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Brush("#CDCDCD"),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 13, 3, 0)
        });
        var copy = Text(detail, 14, Muted);
        copy.TextAlignment = TextAlignment.Center;
        var heading = Text(title, 21, Ink, FontWeight.SemiBold);
        heading.TextAlignment = TextAlignment.Center;
        var content = new StackPanel { Spacing = 10, MaxWidth = 370, Children = { illustration, heading, copy } };
        if (actions.Length != 0)
        {
            var choices = Actions(actions);
            choices.Margin = new Thickness(0, 6, 0, 0);
            choices.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(choices);
        }
        return new Border { Padding = new Thickness(18, 34, 18, 36), MinHeight = 278, Child = content };
    }

    private static Border Card(Control body, double padding = 20) => new()
    {
        Background = Surface, BorderBrush = Line, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(15), Padding = new Thickness(padding), Child = body
    };

    private static StackPanel SettingsGroup(StackPanel page, string title, string? description = null)
    {
        var group = new StackPanel { Spacing = 12, Children = { Text(title, 17, Ink, FontWeight.SemiBold) } };
        if (description is not null)
            group.Children.Add(Text(description, 13, Muted));
        page.Children.Add(Card(group));
        return group;
    }

    private static Control RecordTitle(Control avatar, string title, string? subtitle = null)
    {
        var text = new StackPanel
        {
            Spacing = 3, VerticalAlignment = VerticalAlignment.Center,
            Children = { Text(title, 14, Ink, FontWeight.SemiBold) }
        };
        if (!string.IsNullOrWhiteSpace(subtitle))
            text.Children.Add(Text(subtitle, 12, Muted));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
        row.Children.Add(avatar);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        return row;
    }

    private static Border RecordCard(Control heading, params Control[] actions) => Card(new StackPanel
    {
        Spacing = 12, Children = { heading, Actions(actions) }
    }, 16);

    private static Border BadgeIcon(string glyph, IBrush? background = null, IBrush? foreground = null, double size = 38) => new()
    {
        Width = size, Height = size, CornerRadius = new CornerRadius(size / 3.5),
        Background = background ?? AccentSoft, Child = Icon(glyph, foreground ?? Accent, size * .49)
    };

    private static Border Avatar(string name, double size = 38)
    {
        var initial = System.Globalization.StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();
        var label = Text(initial.Length == 0 ? "?" : initial, size * .37, AccentInk, FontWeight.SemiBold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        return new Border { Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = AccentSoft, Child = label };
    }

    private static Control Brand(bool onDarkSurface = false) => Inline(
        BadgeIcon(ChatIcon, onDarkSurface ? SidebarInk : Accent, onDarkSurface ? Ink : Brushes.White, 35),
        Text("Egroo", 22, onDarkSurface ? Brushes.White : Ink, FontWeight.Bold));

    private static StackPanel Inline(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        foreach (var control in controls)
        {
            control.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(control);
        }
        return row;
    }

    private Button PrimaryButton(string label, Func<Task> action) => StyledButton(label, action, "primary");
    private Button QuietButton(string label, Func<Task> action) => StyledButton(label, action, "quiet");

    private Button StyledButton(string label, Func<Task> action, string style, string? additionalStyle = null, bool serialize = true)
    {
        var button = Button(label, action, serialize);
        button.Classes.Add(style);
        if (additionalStyle is not null)
            button.Classes.Add(additionalStyle);
        return button;
    }

    private Button IconButton(string label, string glyph, Func<Task> action, string style = "quiet")
    {
        var button = StyledButton(label, action, style);
        var foreground = style == "primary" ? Brushes.White : style == "sidebar-action" ? SidebarInk : AccentInk;
        button.Content = Inline(Icon(glyph, foreground, 17), Text(label, 13, foreground, FontWeight.SemiBold));
        AutomationProperties.SetName(button, label);
        return button;
    }

    private Button GlyphButton(string label, string glyph, Func<Task> action, string style = "quiet")
    {
        var button = StyledButton(label, action, style);
        button.Padding = new Thickness(8);
        button.Content = Icon(glyph, style == "sidebar-action" ? SidebarInk : AccentInk, 17);
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, label);
        return button;
    }

    private static IconPath Icon(string glyph, IBrush brush, double size) => new()
    {
        Width = size, Height = size, Data = StreamGeometry.Parse(glyph), Stretch = Stretch.Uniform,
        Stroke = brush, StrokeThickness = 1.55, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };

    private static StackPanel LabeledField(string label, Control field)
    {
        AutomationProperties.SetName(field, label);
        if (field is TextBox textBox && string.Equals(textBox.PlaceholderText, label, StringComparison.OrdinalIgnoreCase))
            textBox.PlaceholderText = null;
        return new StackPanel { Spacing = 5, Children = { Text(label, 13, Ink, FontWeight.SemiBold), field } };
    }

    private static TextBlock Text(string? value, double size = 14, IBrush? color = null, FontWeight? weight = null) => new()
    {
        Text = value, FontSize = size, Foreground = color ?? Ink, FontWeight = weight ?? FontWeight.Normal,
        TextWrapping = TextWrapping.Wrap
    };

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
