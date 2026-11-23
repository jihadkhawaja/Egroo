using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private StackPanel ConversationHeading(string title, string backLabel, Func<Task> back, Control badge, string subtitle)
    {
        var name = Text(title, 23, Ink, FontWeight.Bold);
        var details = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center,
            Children = { name, Text(subtitle, 12, Muted) } };
        var identity = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = 10 };
        identity.Children.Add(GlyphButton($"Back to {backLabel.ToLowerInvariant()}", BackIcon, back));
        Grid.SetColumn(badge, 1);
        identity.Children.Add(badge);
        Grid.SetColumn(details, 2);
        identity.Children.Add(details);
        return new StackPanel { Spacing = 8, Children = { identity } };
    }

    private Button MenuButton(string label, params (string Text, Func<Task> Action)[] entries)
    {
        var menu = new StackPanel { Spacing = 4 };
        var flyout = new Flyout { Content = menu };
        foreach (var (text, action) in entries)
        {
            var item = QuietButton(text, async () =>
            {
                flyout.Hide();
                await action();
            });
            item.HorizontalAlignment = HorizontalAlignment.Stretch;
            item.HorizontalContentAlignment = HorizontalAlignment.Left;
            menu.Children.Add(item);
        }
        return new Button { Content = label, Flyout = flyout, Classes = { "workspace-action", "quiet" } };
    }

    private ScrollViewer ShowConversation(Control header, Panel messages, Control composer, Control? typing = null)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 8, MaxWidth = 1150,
            Classes = { "conversation" } };
        grid.Children.Add(Card(header, 12));
        messages.Margin = new Thickness(2, 10);
        var timeline = new ScrollViewer { Content = messages, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(timeline, 1);
        grid.Children.Add(timeline);

        var footer = new StackPanel { Spacing = 5 };
        if (typing is not null)
            footer.Children.Add(typing);
        footer.Children.Add(composer);
        Grid.SetRow(footer, 2);
        grid.Children.Add(footer);
        _body.Content = grid;
        return timeline;
    }

    private static TextBox ComposerField()
    {
        var input = Field("Write a message…");
        AutomationProperties.SetName(input, "Message");
        input.MinHeight = 48;
        input.MaxHeight = 120;
        input.AcceptsReturn = true;
        input.TextWrapping = TextWrapping.Wrap;
        return input;
    }

    private void SubmitMessageOnEnter(TextBox input, Func<Task> send) => input.KeyDown += (_, args) =>
    {
        if (args.Key == Key.Enter && !args.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            args.Handled = true;
            _ = RunAsync(send);
        }
    };

    private Border ComposerPanel(TextBox input, Func<Task> send, TextBlock attachmentStatus, params Control[] tools)
    {
        var sendButton = IconButton("Send", SendIcon, send, "primary");
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        toolbar.Children.Add(Actions(tools));
        Grid.SetColumn(sendButton, 1);
        sendButton.VerticalAlignment = VerticalAlignment.Bottom;
        toolbar.Children.Add(sendButton);
        return Card(new StackPanel { Spacing = 6, Children = { attachmentStatus, input, toolbar } }, 10);
    }

    private static Grid TimelineDate(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var label = date == today ? "Today" : date == today.AddDays(-1) ? "Yesterday" : date.ToString("d");
        var dateLabel = Text(label, 12, Muted, FontWeight.Medium);
        var left = new Border { Height = 1, Background = Line, VerticalAlignment = VerticalAlignment.Center };
        var right = new Border { Height = 1, Background = Line, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(dateLabel, 1);
        Grid.SetColumn(right, 2);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), ColumnSpacing = 10,
            Margin = new Thickness(0, 4), Children = { left, dateLabel, right } };
    }

    private static void ScrollToLatest(ScrollViewer? timeline)
    {
        if (timeline is not null)
            Dispatcher.UIThread.Post(timeline.ScrollToEnd, DispatcherPriority.Loaded);
    }
}
