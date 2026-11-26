using Avalonia.Controls;
using Egroo.Contracts.Models;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private async Task AddIpRegionSettingsAsync(StackPanel settings)
    {
        var api = _session.IpRegionSecurity;
        IpRegionSettings.Status initial;
        try
        {
            initial = await api.GetIpRegionStatus();
        }
        catch (HttpRequestException)
        {
            return;
        }

        if (!initial.Available && !initial.Enabled)
            return;

        var group = SettingsGroup(settings, "Sign-in regions");
        var details = new StackPanel { Spacing = 10 };
        group.Children.Add(details);
        ShowOverview(initial);

        void ShowOverview(IpRegionSettings.Status status)
        {
            details.Children.Clear();
            details.Children.Add(Label(status.Enabled
                ? $"Allowed regions {string.Join(", ", status.AllowedRegions)}"
                : "Region protection is off."));
            if (status.CurrentRegion is { } current)
                details.Children.Add(Label($"Current region {current}"));

            details.Children.Add(Button("Manage sign-in regions", async () => ShowEditor(await api.GetIpRegionStatus())));
        }

        void ShowEditor(IpRegionSettings.Status status)
        {
            details.Children.Clear();
            details.Children.Add(Label("Keep your one-time recovery codes accessible when you travel. A new sign-in from another or unknown region requires one."));
            if (!status.CanEnable && !status.Enabled)
            {
                details.Children.Add(Label("Enable two-factor authentication, save your recovery codes, and connect from a configured region."));
                details.Children.Add(QuietButton("Back", () => { ShowOverview(status); return Task.CompletedTask; }));
                return;
            }

            var selections = status.Regions.Select(region => new CheckBox
            {
                Content = region,
                IsChecked = region == status.CurrentRegion || status.AllowedRegions.Contains(region, StringComparer.Ordinal)
            }).ToArray();
            if (status.CanEnable)
                details.Children.Add(LabeledField("Allowed regions", Actions(selections)));
            else
                details.Children.Add(Label("Connect from a configured region to change the allowed regions. You can turn off the restriction here."));

            var password = Field("", secret: true);
            var factor = VerificationCodeField();
            details.Children.Add(LabeledField("Current password", password));
            if (status.CanEnable)
                details.Children.Add(Label("Use an authenticator code to save allowed regions."));
            details.Children.Add(LabeledField("Authenticator or recovery code", factor));

            async Task UpdateAsync(string[] regions)
            {
                var request = new IpRegionSettings.Update(
                    ReadRequired(password, "Enter your current password."),
                    ReadRequired(factor, regions.Length > 0
                        ? "Enter the six-digit authenticator code."
                        : "Enter an authenticator code or a recovery code."),
                    regions);
                password.Text = factor.Text = null;
                var result = await api.UpdateIpRegionSettings(request);
                if (!result.Success)
                    throw new InvalidOperationException(result.Message ?? "Could not update sign-in regions.");

                ShowOverview(await api.GetIpRegionStatus());
                Notify(regions.Length == 0 ? "Region protection is off." : "Sign-in regions saved.");
            }

            var buttons = Actions();
            if (status.CanEnable)
                buttons.Children.Add(PrimaryButton("Save allowed regions", () =>
                {
                    var selected = selections.Where(box => box.IsChecked == true).Select(box => (string)box.Content!).ToArray();
                    if (selected.Length == 0)
                        throw new InvalidOperationException("Choose at least your current region.");
                    return UpdateAsync(selected);
                }));
            if (status.Enabled)
                buttons.Children.Add(Button("Turn off region protection", () => UpdateAsync([])));
            buttons.Children.Add(QuietButton("Back", () =>
            {
                password.Text = factor.Text = null;
                ShowOverview(status);
                return Task.CompletedTask;
            }));
            details.Children.Add(buttons);
        }
    }
}
