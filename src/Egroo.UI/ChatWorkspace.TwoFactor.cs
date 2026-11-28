using Avalonia.Controls;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private async Task AddTwoFactorSettingsAsync(StackPanel settings)
    {
        var group = SettingsGroup(settings, "Two-factor authentication");
        var details = new StackPanel { Spacing = 10 };
        group.Children.Add(details);
        try
        {
            var status = await _session.AccountSecurity.GetTwoFactorStatus();
            ShowStatus(status.Enabled);
        }
        catch (HttpRequestException)
        {
            details.Children.Add(Label("Two-factor settings are unavailable on this server."));
        }

        void ShowStatus(bool enabled, string[]? recoveryCodes = null)
        {
            details.Children.Clear();
            details.Children.Add(Label(enabled
                ? "Your next sign-in will require an authenticator or recovery code."
                : "Use an authenticator app to add a second step to sign-in."));
            if (recoveryCodes is not null)
            {
                details.Children.Add(Label("Save these one-time recovery codes somewhere safe. They are shown only once."));
                var codes = Field("", string.Join(Environment.NewLine, recoveryCodes));
                codes.IsReadOnly = true;
                codes.AcceptsReturn = true;
                details.Children.Add(LabeledField("Recovery codes", codes));
                details.Children.Add(QuietButton("I saved my codes", () =>
                {
                    codes.Text = null;
                    ShowStatus(enabled: true);
                    return Task.CompletedTask;
                }));
            }

            var password = Field("", secret: true);
            details.Children.Add(LabeledField("Current password", password));
            if (!enabled)
            {
                async Task BeginAsync()
                {
                    var result = await _session.AccountSecurity.BeginTwoFactorEnrollment(ReadRequired(password, "Enter your current password."));
                    password.Text = null;
                    if (!result.Success || result.SharedKey is not { } key)
                        throw new InvalidOperationException(result.Message ?? "Could not start two-factor setup.");
                    ShowEnrollment(key);
                }

                details.Children.Add(PrimaryButton("Set up authenticator", BeginAsync));
                SubmitOnEnter(password, BeginAsync);
                return;
            }

            var factor = VerificationCodeField();
            details.Children.Add(LabeledField("Authenticator or recovery code", factor));
            async Task DisableAsync()
            {
                var result = await _session.AccountSecurity.DisableTwoFactor(
                    ReadRequired(password, "Enter your current password."),
                    ReadRequired(factor, "Enter an authenticator code or a recovery code."));
                password.Text = factor.Text = null;
                if (!result.Success)
                    throw new InvalidOperationException(result.Message ?? "Could not turn off two-factor authentication.");
                ShowStatus(enabled: false);
                Notify("Two-factor authentication is off.");
            }

            details.Children.Add(Button("Turn off two-factor authentication", DisableAsync));
            SubmitOnEnter(factor, DisableAsync);
        }

        void ShowEnrollment(string key)
        {
            details.Children.Clear();
            details.Children.Add(Label("Add a time-based account in your authenticator app using this setup key."));
            var sharedKey = Field("", key);
            sharedKey.IsReadOnly = true;
            details.Children.Add(LabeledField("Setup key", sharedKey));
            var password = Field("", secret: true);
            var code = VerificationCodeField();
            details.Children.Add(LabeledField("Current password", password));
            details.Children.Add(LabeledField("Six-digit authenticator code", code));

            async Task EnableAsync()
            {
                var result = await _session.AccountSecurity.EnableTwoFactor(
                    ReadRequired(password, "Enter your current password."),
                    ReadRequired(code, "Enter the six-digit authenticator code."));
                password.Text = code.Text = null;
                if (!result.Success || result.RecoveryCodes is not { } recoveryCodes)
                    throw new InvalidOperationException(result.Message ?? "Could not enable two-factor authentication.");
                sharedKey.Text = null;
                ShowStatus(enabled: true, recoveryCodes);
                Notify("Two-factor authentication is on.");
            }

            details.Children.Add(Actions(PrimaryButton("Enable two-factor authentication", EnableAsync),
                QuietButton("Cancel setup", () =>
                {
                    sharedKey.Text = password.Text = code.Text = null;
                    ShowStatus(enabled: false);
                    return Task.CompletedTask;
                })));
            SubmitOnEnter(code, EnableAsync);
            password.Focus();
        }
    }

    private static TextBox VerificationCodeField()
    {
        var field = Field("", secret: true);
        field.MaxLength = 24;
        return field;
    }

    private static string ReadRequired(TextBox input, string prompt) =>
        !string.IsNullOrWhiteSpace(input.Text) ? input.Text : throw new InvalidOperationException(prompt);
}
