using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private async Task OpenPasswordRecoveryAsync(string? username)
    {
        if (!await _session.AccountRecovery.IsPasswordRecoveryAvailable())
            throw new InvalidOperationException("Email recovery is unavailable on this server.");
        ShowPasswordRecovery(username);
    }

    private void ShowPasswordRecovery(string? usernameValue)
    {
        SetWorkspaceOpen(false);
        var form = new StackPanel { Spacing = 16 };
        form.Children.Add(Brand());
        form.Children.Add(Text("Reset your password", 31, Ink, FontWeight.Bold));

        var username = Field("", usernameValue);
        var address = Field("");
        var code = RecoveryEmailCodeField();
        var password = Field("", secret: true);
        var factor = VerificationCodeField();

        form.Children.Add(LabeledField("Username", username));
        var addressRow = LabeledField("Verified recovery email", address);
        form.Children.Add(addressRow);
        var resetFields = new StackPanel { Spacing = 12, IsVisible = false };
        resetFields.Children.Add(LabeledField("Email recovery code", code));
        resetFields.Children.Add(LabeledField("New password", password));
        var factorRow = LabeledField("Authenticator or recovery code", factor);
        factorRow.IsVisible = false;
        resetFields.Children.Add(factorRow);

        async Task RequestAsync()
        {
            var result = await _session.AccountRecovery.RequestPasswordRecovery(
                ReadRequired(username, "Enter your username.").Trim(),
                ReadRequired(address, "Enter your verified recovery email.").Trim());
            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? "Could not request an email recovery code.");

            code.Text = factor.Text = null;
            resetFields.IsVisible = true;
            factorRow.IsVisible = false;
            code.Focus();
            Notify(result.Message ?? "If your account matches, a recovery code will be sent.");
        }

        async Task ResetAsync()
        {
            var result = await _session.AccountRecovery.ResetPassword(
                ReadRequired(username, "Enter your username.").Trim(),
                ReadRequired(code, "Enter the code from your recovery email.").Trim(),
                ReadRequired(password, "Enter your new password."), factor.Text);
            factor.Text = null;
            if (result.RequiresTwoFactor)
            {
                factorRow.IsVisible = true;
                factor.Focus();
                Notify(result.Message ?? "Enter an authenticator code or a recovery code.");
                return;
            }

            if (!result.Success)
                throw new InvalidOperationException(result.Message ?? "Could not reset the password.");

            code.Text = password.Text = null;
            ShowAuthentication(usernameValue: username.Text);
            Notify(result.Message ?? "Password reset. Sign in with your new password.");
        }

        var send = Button("Send email recovery code", RequestAsync);
        send.HorizontalAlignment = HorizontalAlignment.Stretch;
        form.Children.Add(send);
        var reset = PrimaryButton("Reset password", ResetAsync);
        reset.HorizontalAlignment = HorizontalAlignment.Stretch;
        resetFields.Children.Add(reset);
        form.Children.Add(resetFields);
        form.Children.Add(QuietButton("Back to sign in", () =>
        {
            address.Text = code.Text = password.Text = factor.Text = null;
            ShowAuthentication(usernameValue: username.Text);
            Notify(string.Empty);
            return Task.CompletedTask;
        }));
        SubmitOnEnter(address, RequestAsync);
        SubmitOnEnter(password, ResetAsync);
        SubmitOnEnter(factor, ResetAsync);
        DisplayAuthenticationForm(form);
    }

    private async Task AddRecoveryEmailSettingsAsync(StackPanel page)
    {
        var group = SettingsGroup(page, "Recovery email");
        var content = new StackPanel { Spacing = 10 };
        group.Children.Add(content);

        AccountRecovery.Status status;
        try
        {
            status = await _session.AccountRecovery.GetRecoveryStatus();
        }
        catch (HttpRequestException)
        {
            content.Children.Add(Label("Email recovery is unavailable on this server."));
            return;
        }

        Render(status);

        void Render(AccountRecovery.Status state)
        {
            content.Children.Clear();
            content.Children.Add(Label(state.HasVerifiedAddress
                ? "You have a verified recovery email."
                : "A separate email address can help you reset your password."));
            if (!state.Available)
                content.Children.Add(Label("This server cannot send recovery emails at the moment."));

            if (state.Available)
            {
                var address = Field("");
                content.Children.Add(LabeledField(state.HasVerifiedAddress ? "New recovery email" : "Recovery email address", address));
                var password = Field("", secret: true);
                var factor = VerificationCodeField();
                content.Children.Add(LabeledField("Current password", password));
                content.Children.Add(LabeledField(state.RequiresTwoFactor ? "Authenticator or recovery code" : "Authenticator or recovery code, if enabled", factor));

                async Task SendAsync()
                {
                    var result = await _session.AccountRecovery.BeginRecoveryEmailEnrollment(
                        ReadRequired(address, "Enter a recovery email address.").Trim(),
                        ReadRequired(password, "Enter your current password."), factor.Text);
                    password.Text = factor.Text = null;
                    if (!result.Success)
                        throw new InvalidOperationException(result.Message ?? "Could not send a confirmation code.");

                    address.Text = null;
                    Render(state with { AwaitingVerification = true });
                    Notify("Check the new address for its confirmation code.");
                }

                content.Children.Add(Button(state.HasVerifiedAddress ? "Send replacement confirmation" : "Send confirmation email", SendAsync));
                if (state.HasVerifiedAddress || state.AwaitingVerification)
                    AddRemoval(password, factor);
            }
            else if (state.HasVerifiedAddress || state.AwaitingVerification)
            {
                var password = Field("", secret: true);
                var factor = VerificationCodeField();
                content.Children.Add(LabeledField("Current password", password));
                content.Children.Add(LabeledField(state.RequiresTwoFactor ? "Authenticator or recovery code" : "Authenticator or recovery code, if enabled", factor));
                AddRemoval(password, factor);
            }

            if (state.AwaitingVerification)
            {
                var code = RecoveryEmailCodeField();
                content.Children.Add(LabeledField("Email confirmation code", code));
                async Task ConfirmAsync()
                {
                    var result = await _session.AccountRecovery.ConfirmRecoveryEmail(
                        ReadRequired(code, "Enter the code from the confirmation email.").Trim());
                    code.Text = null;
                    if (!result.Success)
                        throw new InvalidOperationException(result.Message ?? "Could not confirm the recovery address.");

                    Render(state with { HasVerifiedAddress = true, AwaitingVerification = false });
                    Notify("Recovery email verified.");
                }
                content.Children.Add(PrimaryButton("Verify recovery email", ConfirmAsync));
                SubmitOnEnter(code, ConfirmAsync);
            }

            void AddRemoval(TextBox password, TextBox factor)
            {
                AddConfirmation(content, state.HasVerifiedAddress ? "Remove recovery email" : "Cancel email setup", async () =>
                {
                    var result = await _session.AccountRecovery.RemoveRecoveryEmail(
                        ReadRequired(password, "Enter your current password."), factor.Text);
                    password.Text = factor.Text = null;
                    if (!result.Success)
                        throw new InvalidOperationException(result.Message ?? "Could not remove the recovery address.");

                    Render(state with { HasVerifiedAddress = false, AwaitingVerification = false });
                    Notify(state.HasVerifiedAddress ? "Recovery email removed." : "Recovery email setup canceled.");
                });
            }
        }
    }

    private static TextBox RecoveryEmailCodeField()
    {
        var field = Field("", secret: true);
        field.MaxLength = 32;
        return field;
    }
}
