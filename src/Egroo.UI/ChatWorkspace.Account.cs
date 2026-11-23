using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private Task ShowFriendsAsync() => ShowFriendsAsync(null);

    private async Task ShowFriendsAsync(string? query)
    {
        await CloseChannelViewAsync();
        SelectSection(WorkspaceSection.Friends);
        var user = _session.User!;
        var panel = Page("Friends", "Find people, manage requests, and start direct conversations.");
        var search = Field("Search by username", query);
        Task SearchAsync() => ShowFriendsAsync(search.Text?.Trim());
        SubmitOnEnter(search, SearchAsync);
        var searchButton = IconButton("Search", SearchIcon, SearchAsync);
        var searchBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        searchBar.Children.Add(search);
        Grid.SetColumn(searchButton, 1);
        searchBar.Children.Add(searchButton);
        panel.Children.Add(Card(searchBar, 14));
        if (!string.IsNullOrWhiteSpace(query))
        {
            panel.Children.Add(Actions(Text("Search results", 17, Ink, FontWeight.SemiBold), QuietButton("My friends", ShowFriendsAsync)));
            var results = (await _session.Users.SearchUser(query) ?? [])
                .Where(person => person.Id != user.Id && !string.IsNullOrWhiteSpace(person.Username)).ToArray();
            foreach (var person in results)
            {
                var name = UserName(person);
                panel.Children.Add(RecordCard(RecordTitle(Avatar(name), name, $"@{person.Username}"), Button("Add friend", async () =>
                {
                    await RequireAsync(_session.Users.AddFriend(person.Username!));
                    Notify("Friend request sent.");
                })));
            }
            if (results.Length == 0)
                panel.Children.Add(Card(EmptyState(FriendsIcon, "No matching people", "Try a different username."), 0));
            return;
        }

        var requests = await _session.Users.GetUserFriendRequests(user.Id) ?? [];
        if (requests.Length != 0)
            panel.Children.Add(Text("Friend requests", 17, Ink, FontWeight.SemiBold));
        foreach (var relation in requests)
        {
            var id = OtherUser(relation, user.Id);
            var person = await _session.Users.GetUserPublicDetails(id);
            var name = person is null ? "User" : UserName(person);
            panel.Children.Add(RecordCard(RecordTitle(Avatar(name), name, "Sent you a friend request"),
                PrimaryButton("Accept", async () =>
                {
                    await RequireAsync(_session.Users.AcceptFriend(id));
                    await ShowFriendsAsync();
                }),
                Button("Decline", async () =>
                {
                    await RequireAsync(_session.Users.DenyFriend(id));
                    await ShowFriendsAsync();
                })));
        }
        var relations = await _session.Users.GetUserFriends(user.Id) ?? [];
        if (relations.Length != 0)
            panel.Children.Add(Text("Your friends", 17, Ink, FontWeight.SemiBold));
        foreach (var relation in relations)
        {
            var person = await _session.Users.GetUserPublicDetails(OtherUser(relation, user.Id));
            if (person?.Username is not { } username)
                continue;
            var name = UserName(person);
            panel.Children.Add(RecordCard(RecordTitle(Avatar(name), name, $"@{username} · {(person.IsOnline ? "Online" : "Offline")}"),
                PrimaryButton("Message", async () =>
                {
                    var channel = await _session.Channels.CreateChannel(username);
                    if (channel is null)
                        throw new InvalidOperationException("Could not open the conversation.");
                    await OpenChannelAsync(channel);
                }),
                Button("Remove", async () =>
                {
                    await RequireAsync(_session.Users.RemoveFriend(username));
                    await ShowFriendsAsync();
                })));
        }
        if (requests.Length == 0 && relations.Length == 0)
            panel.Children.Add(Card(EmptyState(FriendsIcon, "No friends yet", "Search for a username to send your first friend request."), 0));
    }

    private async Task ShowSettingsAsync()
    {
        await CloseChannelViewAsync();
        SelectSection(WorkspaceSection.Settings);
        var panel = Page("Settings", "Manage your profile, password, and encryption devices.");
        var user = await _session.Users.GetUserPrivateDetails() ?? _session.User!;
        var display = Field("Display name", user.UserDetail?.DisplayName);
        var email = Field("Email", user.UserDetail?.Email);
        var first = Field("First name", user.UserDetail?.FirstName);
        var last = Field("Last name", user.UserDetail?.LastName);
        var profile = SettingsGroup(panel, "Profile");
        profile.Children.Add(Actions(LabeledField("Display name", display), LabeledField("Email", email),
            LabeledField("First name", first), LabeledField("Last name", last)));
        profile.Children.Add(PrimaryButton("Save profile", async () =>
        {
            await RequireAsync(_session.Users.UpdateDetails(display.Text, email.Text, first.Text, last.Text));
            Notify("Profile saved.");
        }));
        profile.Children.Add(Actions(Button("Change avatar", () => UploadProfileImageAsync(true)),
            Button("Change cover", () => UploadProfileImageAsync(false)),
            Button("Remove avatar", () => RequireAsync(_session.Users.UpdateAvatar(null))),
            Button("Remove cover", () => RequireAsync(_session.Users.UpdateCover(null)))));

        var password = Field("Current password", secret: true);
        var newPassword = Field("New password", secret: true);
        var security = SettingsGroup(panel, "Password");
        security.Children.Add(Actions(LabeledField("Current password", password), LabeledField("New password", newPassword)));
        security.Children.Add(Button("Change password", async () =>
        {
            var result = await _session.ChangePasswordAsync(password.Text ?? "", newPassword.Text ?? "");
            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? "Could not change the password.");
            password.Text = newPassword.Text = null;
            Notify("Password changed.");
        }));

        var identity = await _session.Encryption.GetLocalIdentityAsync();
        var devices = SettingsGroup(panel, "Encryption devices", "Manage the devices that can decrypt messages sent to you.");
        var keys = await _session.Users.GetEncryptionKeys() ?? [];
        foreach (var key in keys)
        {
            devices.Children.Add(Actions(Label($"{key.DeviceLabel ?? key.KeyId}{(key.KeyId == identity?.KeyId ? " · This device" : "")}"),
                Button("Remove device", async () =>
                {
                    await RequireAsync(_session.Users.RemoveEncryptionKey(key.KeyId));
                    await ShowSettingsAsync();
                })));
        }
        AddConfirmation(devices, "Regenerate this device’s key", async () =>
        {
            await _session.Encryption.RegenerateIdentityAsync();
            await ShowSettingsAsync();
        });

        var feedback = Field("Tell us what you think");
        feedback.AcceptsReturn = true;
        feedback.TextWrapping = TextWrapping.Wrap;
        feedback.MinHeight = 76;
        var help = SettingsGroup(panel, "Feedback");
        help.Children.Add(feedback);
        help.Children.Add(Button("Send feedback", async () =>
        {
            await RequireAsync(_session.Users.SendFeedback(feedback.Text ?? string.Empty));
            feedback.Text = null;
            Notify("Feedback sent.");
        }));
        var account = SettingsGroup(panel, "Account");
        account.Children.Add(IconButton("Sign out", SignOutIcon, SignOutAsync));
        AddConfirmation(account, "Delete account", async () =>
        {
            await RequireAsync(_session.Users.DeleteUser());
            await SignOutAsync();
        });
    }

    private async Task UploadProfileImageAsync(bool avatar)
    {
        using var image = await PickFileAsync(avatar ? "Choose an avatar" : "Choose a cover");
        if (image is null)
            return;
        var bytes = await ReadFileAsync(image, MaxAttachmentBytes);
        var data = $"data:{FileContentType(image.Name)};base64,{Convert.ToBase64String(bytes)}";
        await RequireAsync(avatar ? _session.Users.UpdateAvatar(data) : _session.Users.UpdateCover(data));
        Notify("Image saved.");
    }

    private static Guid OtherUser(UserFriend relation, Guid user) =>
        relation.UserId == user ? relation.FriendUserId : relation.UserId;

    private static string FileContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".txt" or ".md" or ".csv" or ".log" => "text/plain",
        ".json" => "application/json",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    private void AddConfirmation(StackPanel panel, string label, Func<Task> action)
    {
        var row = Actions();
        var confirm = StyledButton($"Confirm {label.ToLowerInvariant()}", action, "danger");
        confirm.IsVisible = false;
        row.Children.Add(StyledButton(label, () =>
        {
            confirm.IsVisible = !confirm.IsVisible;
            return Task.CompletedTask;
        }, "danger"));
        row.Children.Add(confirm);
        panel.Children.Add(row);
    }

    private static async Task RequireAsync(Task<bool> action)
    {
        if (!await action)
            throw new InvalidOperationException("The server rejected this change.");
    }
}
