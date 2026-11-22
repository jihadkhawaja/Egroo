using Avalonia.Controls;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private Task ShowFriendsAsync() => ShowFriendsAsync(null);

    private async Task ShowFriendsAsync(string? query)
    {
        _channel = null;
        var user = _session.User!;
        var panel = Page("Friends");
        var search = Field("Find people", query);
        panel.Children.Add(Actions(search, Button("Search", () => ShowFriendsAsync(search.Text)), Button("My friends", ShowFriendsAsync)));
        if (!string.IsNullOrWhiteSpace(query))
        {
            foreach (var person in await _session.Users.SearchUser(query) ?? [])
            {
                if (person.Id == user.Id || string.IsNullOrWhiteSpace(person.Username))
                    continue;
                panel.Children.Add(Actions(Label(UserName(person)), Button("Add friend", async () =>
                {
                    await RequireAsync(_session.Users.AddFriend(person.Username));
                    Notify("Friend request sent.");
                })));
            }
            return;
        }

        foreach (var relation in await _session.Users.GetUserFriendRequests(user.Id) ?? [])
        {
            var id = OtherUser(relation, user.Id);
            var person = await _session.Users.GetUserPublicDetails(id);
            panel.Children.Add(Actions(Label($"{person?.Username ?? "User"} sent a request"),
                Button("Accept", async () =>
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
        foreach (var relation in await _session.Users.GetUserFriends(user.Id) ?? [])
        {
            var person = await _session.Users.GetUserPublicDetails(OtherUser(relation, user.Id));
            if (person?.Username is not { } username)
                continue;
            panel.Children.Add(Actions(Label($"{UserName(person)} · {(person.IsOnline ? "Online" : "Offline")}"),
                Button("Message", async () =>
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
    }

    private async Task ShowSettingsAsync()
    {
        _channel = null;
        var panel = Page("Settings");
        var user = await _session.Users.GetUserPrivateDetails() ?? _session.User!;
        var display = Field("Display name", user.UserDetail?.DisplayName);
        var email = Field("Email", user.UserDetail?.Email);
        var first = Field("First name", user.UserDetail?.FirstName);
        var last = Field("Last name", user.UserDetail?.LastName);
        panel.Children.Add(Actions(display, email, first, last));
        panel.Children.Add(Button("Save profile", async () =>
        {
            await RequireAsync(_session.Users.UpdateDetails(display.Text, email.Text, first.Text, last.Text));
            Notify("Profile saved.");
        }));
        panel.Children.Add(Actions(Button("Change avatar", () => UploadProfileImageAsync(true)),
            Button("Change cover", () => UploadProfileImageAsync(false)),
            Button("Remove avatar", () => RequireAsync(_session.Users.UpdateAvatar(null))),
            Button("Remove cover", () => RequireAsync(_session.Users.UpdateCover(null)))));

        var password = Field("Current password", secret: true);
        var newPassword = Field("New password", secret: true);
        panel.Children.Add(Actions(password, newPassword, Button("Change password", async () =>
        {
            var result = await _session.ChangePasswordAsync(password.Text ?? "", newPassword.Text ?? "");
            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? "Could not change the password.");
            password.Text = newPassword.Text = null;
            Notify("Password changed.");
        })));

        var identity = await _session.Encryption.GetLocalIdentityAsync();
        panel.Children.Add(Label("Encryption devices"));
        foreach (var key in await _session.Users.GetEncryptionKeys() ?? [])
        {
            panel.Children.Add(Actions(Label($"{key.DeviceLabel ?? key.KeyId}{(key.KeyId == identity?.KeyId ? " · This device" : "")}"),
                Button("Remove device", async () =>
                {
                    await RequireAsync(_session.Users.RemoveEncryptionKey(key.KeyId));
                    await ShowSettingsAsync();
                })));
        }
        AddConfirmation(panel, "Regenerate this device’s key", async () =>
        {
            await _session.Encryption.RegenerateIdentityAsync();
            await ShowSettingsAsync();
        });

        var feedback = Field("Feedback");
        panel.Children.Add(Actions(feedback, Button("Send feedback", async () =>
        {
            await RequireAsync(_session.Users.SendFeedback(feedback.Text ?? string.Empty));
            feedback.Text = null;
            Notify("Feedback sent.");
        })));
        AddConfirmation(panel, "Delete account", async () =>
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
        var confirm = Button($"Confirm {label.ToLowerInvariant()}", action);
        confirm.IsVisible = false;
        row.Children.Add(Button(label, () =>
        {
            confirm.IsVisible = !confirm.IsVisible;
            return Task.CompletedTask;
        }));
        row.Children.Add(confirm);
        panel.Children.Add(row);
    }

    private static async Task RequireAsync(Task<bool> action)
    {
        if (!await action)
            throw new InvalidOperationException("The server rejected this change.");
    }
}
