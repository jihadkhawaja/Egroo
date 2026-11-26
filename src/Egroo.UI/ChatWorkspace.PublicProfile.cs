using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private readonly Border _publicProfileView = new() { Background = Canvas, IsVisible = false };

    private Button PublicProfileButton(Guid userId, string name, bool inMessage = false)
    {
        Task OpenAsync() => ShowPublicProfileAsync(userId, inMessage ? "conversation" : "friends");
        var button = inMessage ? MessageAction(name, OpenAsync) : QuietButton("View profile", OpenAsync);
        AutomationProperties.SetName(button, $"View profile for {name}");
        if (inMessage)
            ToolTip.SetTip(button, $"View profile for {name}");
        return button;
    }

    private Task ShowPublicProfileAsync(Guid userId, string originName)
    {
        if (userId == Guid.Empty || _session.User is not { } viewer || _body.Content is not Control origin)
            return Task.CompletedTask;

        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var scroll = origin as ScrollViewer ?? (origin as Grid)?.Children.OfType<ScrollViewer>().FirstOrDefault();
        var offset = scroll?.Offset;
        var back = IconButton("Back", BackIcon, () =>
        {
            ClosePublicProfile();
            focus?.Focus();
            if (offset is { } position && scroll is not null)
                scroll.Offset = position;
            return Task.CompletedTask;
        });
        AutomationProperties.SetName(back, $"Back to {originName}");
        var content = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var page = new StackPanel { Spacing = 18, MaxWidth = 1150, Classes = { "workspace-page" } };
        page.Children.Add(PageHeading("Profile", null, back));
        page.Children.Add(content);
        var view = new ScrollViewer { Content = page };
        _publicProfileView.Child = view;
        _publicProfileView.IsVisible = true;
        _body.IsEnabled = false;

        bool IsCurrent() => view == _publicProfileView.Child && _session.User?.Id == viewer.Id;

        async Task LoadAsync()
        {
            content.Content = Card(Text("Loading profile…", 14, Muted));
            UserDto? profile;
            try
            {
                profile = await _session.Users.GetUserPublicDetails(userId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                if (IsCurrent())
                {
                    var retry = QuietButton("Try again", () =>
                    {
                        _ = LoadAsync();
                        return Task.CompletedTask;
                    });
                    content.Content = Card(EmptyState(FriendsIcon, "Couldn’t load profile", "Check your connection and try again.", retry), 0);
                }
                return;
            }

            if (!IsCurrent())
                return;
            if (string.IsNullOrWhiteSpace(profile?.Username))
            {
                content.Content = Card(EmptyState(FriendsIcon, "Profile unavailable", "This profile could not be found."), 0);
                return;
            }

            var username = profile.Username;
            var displayName = string.IsNullOrWhiteSpace(profile.UserDetail?.DisplayName) ? username : profile.UserDetail.DisplayName;
            var cover = ProfileImage();
            var avatar = ProfileImage();
            var initials = Avatar(displayName, 64);
            var banner = new Border
            {
                Height = 144, Background = AccentSoft, CornerRadius = new CornerRadius(10),
                ClipToBounds = true, Child = cover
            };
            var portrait = new Grid
            {
                Width = 64, Height = 64, VerticalAlignment = VerticalAlignment.Center,
                Children = { initials, new Border { CornerRadius = new CornerRadius(32), ClipToBounds = true, Child = avatar } }
            };
            var name = Text(displayName, 23, Ink, FontWeight.Bold);
            AutomationProperties.SetHeadingLevel(name, 2);
            var identity = new StackPanel
            {
                Spacing = 4, VerticalAlignment = VerticalAlignment.Center,
                Children = { name, Text($"@{username}", 14, Muted) }
            };
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
            heading.Children.Add(portrait);
            Grid.SetColumn(identity, 1);
            heading.Children.Add(identity);
            content.Content = Card(new StackPanel { Spacing = 16, Children = { banner, heading } }, 16);

            await Task.WhenAll(
                LoadProfileImageAsync(() => _session.Users.GetAvatar(userId), avatar, 128, IsCurrent, initials),
                LoadProfileImageAsync(() => _session.Users.GetCover(userId), cover, 960, IsCurrent));
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (IsCurrent())
                back.Focus();
        }, DispatcherPriority.Loaded);
        _ = LoadAsync();
        return Task.CompletedTask;
    }

    private void ClosePublicProfile()
    {
        if (!_publicProfileView.IsVisible)
            return;
        _publicProfileView.IsVisible = false;
        _publicProfileView.Child = null;
        _body.IsEnabled = true;
    }

    private static Image ProfileImage()
    {
        var image = new Image { Stretch = Stretch.UniformToFill, IsVisible = false };
        image.DetachedFromVisualTree += (_, _) =>
        {
            if (image.Source is not Bitmap bitmap)
                return;
            image.Source = null;
            image.IsVisible = false;
            bitmap.Dispose();
        };
        return image;
    }

    private static async Task LoadProfileImageAsync(Func<Task<MediaResult?>> read, Image image, int width,
        Func<bool> isCurrent, Control? fallback = null)
    {
        try
        {
            var media = await read();
            if (!isCurrent() || string.IsNullOrWhiteSpace(media?.ImageBase64))
                return;
            using var stream = new MemoryStream(Convert.FromBase64String(media.ImageBase64));
            image.Source = Bitmap.DecodeToWidth(stream, width);
            image.IsVisible = true;
            if (fallback is not null)
                fallback.IsVisible = false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
