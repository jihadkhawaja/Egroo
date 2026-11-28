using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Egroo.UI.Services;

namespace Egroo.UI;

public sealed partial class ChatWorkspace
{
    private readonly NearbySessionService _nearby;
    private readonly DispatcherTimer _nearbyTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Func<Task>? _refreshNearby;
    private bool _nearbyOpen;
    private bool _nearbyBusy;
    private Task? _nearbyPoll;

    private enum NearbyPairingStep { Choose, EnterInvitation, EnterReply, Verify, Ended }

    private async Task ShowNearbyAsync()
    {
        if (_nearbyOpen || (_workspaceOpen && _loading))
            return;

        _nearbyOpen = true;
        SetWorkspaceOpen(false);
        SetNotice(string.Empty, nearby: true);
        ShowNearbyPairing();
        await CloseChannelViewAsync();
    }

    private void ShowNearbyPairing()
    {
        var step = NearbyPairingStep.Choose;
        var usedRuntime = false;
        var working = Label(null);
        working.IsVisible = false;
        AutomationProperties.SetLiveSetting(working, AutomationLiveSetting.Polite);
        var flow = new StackPanel { Spacing = 14 };
        var back = NearbyBackButton(() => ExitNearbyAsync(usedRuntime));
        var page = Page("Nearby chat", "Use the same Wi-Fi, hotspot, or wired network.", back: back);
        page.MaxWidth = 740;
        page.Children.Add(working);
        page.Children.Add(flow);

        var outgoing = Field("");
        outgoing.IsReadOnly = true;
        var incoming = Field("Paste the full code here");
        incoming.MaxLength = 32768;
        incoming.MaxHeight = 98;
        incoming.TextWrapping = TextWrapping.Wrap;
        ScrollViewer.SetVerticalScrollBarVisibility(incoming, ScrollBarVisibility.Auto);

        var shareTitle = Text("Share your invitation", 18, Ink, FontWeight.SemiBold);
        var shareDetail = Label(null);
        var copy = NearbyButton("Copy invitation", async () =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
                throw new InvalidOperationException("Select and copy the code directly from the field.");
            await clipboard.SetTextAsync(outgoing.Text);
            SetNotice(step == NearbyPairingStep.EnterReply ? "Invitation copied." : "Reply copied.", nearby: true);
        });
        var share = Card(new StackPanel { Spacing = 10, Children = { shareTitle, shareDetail, outgoing, copy } });

        var entryTitle = Text("Enter their invitation", 18, Ink, FontWeight.SemiBold);
        var entryDetail = Label(null);
        var paste = NearbyButton("Paste invitation", async () =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            var code = clipboard is null ? null : await clipboard.TryGetTextAsync();
            if (string.IsNullOrWhiteSpace(code))
                throw new InvalidOperationException("Copy the code on this device, then paste it into the field.");
            if (code.Trim().Length > incoming.MaxLength)
                throw new InvalidOperationException("This code is too long. Ask the other person to copy it again.");
            incoming.Text = code.Trim();
        });
        var submit = NearbyButton("Create reply", "primary");
        incoming.TextChanged += (_, _) => submit.IsEnabled = !string.IsNullOrWhiteSpace(incoming.Text);
        submit.IsEnabled = false;
        var entry = Card(new StackPanel { Spacing = 10, Children = { entryTitle, entryDetail, incoming, Actions(paste, submit) } });

        var invite = NearbyButton("Create invitation", "primary");
        var join = NearbyButton("I have an invitation", "quiet");
        var choices = Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Text("Pair two devices", 21, Ink, FontWeight.SemiBold),
                Label("Open Nearby chat on both devices. One person creates an invitation and the other sends back a reply. No account is needed."),
                Actions(invite, join)
            }
        });

        var verificationCode = Text(null, 20, Ink, FontWeight.SemiBold);
        var verificationStatus = Label(null);
        AutomationProperties.SetLiveSetting(verificationStatus, AutomationLiveSetting.Polite);
        var confirm = NearbyButton("Codes match", async () =>
        {
            await _nearby.ConfirmAsync();
            await StartWatchingAsync();
        }, "primary");
        var mismatch = NearbyButton("Codes don’t match", async () =>
        {
            await RestartPairingAsync(usedRuntime);
            SetNotice("Pairing canceled. Check who sent the invitation before starting again.", nearby: true);
        });
        var verification = Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Text("Verify the connection", 18, Ink, FontWeight.SemiBold),
                Label("Compare every group below in person or over a channel you already trust. Both people need to confirm."),
                verificationCode,
                verificationStatus,
                Actions(confirm, mismatch)
            }
        });
        var endedReason = Label(null);
        var ended = Card(new StackPanel
        {
            Spacing = 8,
            Children = { Text("Pairing ended", 18, Ink, FontWeight.SemiBold), endedReason }
        });
        var restart = NearbyButton("Start over", () => RestartPairingAsync(usedRuntime));
        var retry = NearbyButton("Retry connection check", StartWatchingAsync);
        WatchNearby(retry, UpdatePairing);

        BindNearbyAction(submit, async () =>
        {
            usedRuntime = true;
            var acceptingReply = step == NearbyPairingStep.EnterReply;
            await WorkAsync(acceptingReply ? "Checking reply…" : "Creating your reply…", async () =>
            {
                var code = incoming.Text?.Trim() ?? "";
                if (acceptingReply)
                {
                    await _nearby.AcceptAsync(code);
                    outgoing.Text = null;
                }
                else
                    outgoing.Text = await _nearby.ReplyAsync(code);
                incoming.Text = null;
                SetStep(NearbyPairingStep.Verify);
                await StartWatchingAsync();
            });
            if (share.IsVisible)
                copy.Focus();
            else if (verification.IsVisible)
                confirm.Focus();
        });

        BindNearbyAction(invite, async () =>
        {
            usedRuntime = true;
            await WorkAsync("Creating your invitation…", async () =>
            {
                outgoing.Text = await _nearby.InviteAsync();
                SetStep(NearbyPairingStep.EnterReply);
                await StartWatchingAsync();
            });
            if (share.IsVisible)
                copy.Focus();
        });
        BindNearbyAction(join, () =>
        {
            SetStep(NearbyPairingStep.EnterInvitation);
            incoming.Focus();
            return Task.CompletedTask;
        });
        flow.Children.Add(choices);
        flow.Children.Add(share);
        flow.Children.Add(entry);
        flow.Children.Add(verification);
        flow.Children.Add(ended);
        flow.Children.Add(restart);
        flow.Children.Add(retry);
        page.Children.Add(new Expander
        {
            Header = "Connection help",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Label("Use a network that lets devices reach each other. Guest Wi-Fi can block this. Try a personal hotspot and allow local network access if prompted."),
                    Label("Pairing codes expire after five minutes. If a code expires, start over on both devices.")
                }
            }
        });
        SetStep(NearbyPairingStep.Choose);

        async Task WorkAsync(string message, Func<Task> action)
        {
            working.Text = message;
            working.IsVisible = true;
            flow.IsEnabled = back.IsEnabled = false;
            try { await action(); }
            finally
            {
                flow.IsEnabled = back.IsEnabled = true;
                working.IsVisible = false;
            }
        }

        void SetStep(NearbyPairingStep next)
        {
            step = next;
            var enteringReply = step == NearbyPairingStep.EnterReply;
            var sharingReply = step == NearbyPairingStep.Verify && !string.IsNullOrEmpty(outgoing.Text);
            choices.IsVisible = step == NearbyPairingStep.Choose;
            share.IsVisible = enteringReply || sharingReply;
            entry.IsVisible = enteringReply || step == NearbyPairingStep.EnterInvitation;
            verification.IsVisible = false;
            ended.IsVisible = step == NearbyPairingStep.Ended;
            restart.IsVisible = step != NearbyPairingStep.Choose;

            shareTitle.Text = sharingReply ? "Send back your reply" : "Share your invitation";
            shareDetail.Text = sharingReply
                ? "Send this reply to the person who invited you. They’ll use it to see the same verification code."
                : "Send this invitation to the other person and ask them to open Nearby chat and select “I have an invitation.”";
            copy.Content = sharingReply ? "Copy reply" : "Copy invitation";
            AutomationProperties.SetName(outgoing, sharingReply ? "Reply code" : "Invitation code");
            entryTitle.Text = enteringReply ? "Enter their reply" : "Enter their invitation";
            entryDetail.Text = enteringReply
                ? "Paste the reply they send back to move on to verification."
                : "Paste the invitation from the other person. You’ll get a reply to send back.";
            paste.Content = enteringReply ? "Paste reply" : "Paste invitation";
            submit.Content = enteringReply ? "Continue to verification" : "Create reply";
            AutomationProperties.SetName(incoming, enteringReply ? "Received reply code" : "Received invitation code");
        }

        void UpdatePairing(NearbySnapshot? state)
        {
            if (state?.WasConnected == true)
            {
                ShowNearbyConversation(state);
                return;
            }

            if (state?.Closed == true)
            {
                SetStep(NearbyPairingStep.Ended);
                endedReason.Text = state.Reason ?? "Start over to get a fresh pairing code.";
                _nearbyTimer.Stop();
                return;
            }

            if (state?.VerificationCode is not { } code)
                return;
            verification.IsVisible = true;
            restart.IsVisible = false;
            verificationCode.Text = code;
            verificationStatus.Text = state.Confirmed ? "You confirmed the code. Waiting for the other device." : null;
            verificationStatus.IsVisible = state.Confirmed;
            confirm.IsVisible = !state.Confirmed;
        }
    }

    private void ShowNearbyConversation(NearbySnapshot initialState)
    {
        var empty = Card(new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Text("You’re connected", 18, Ink, FontWeight.SemiBold),
                Label("Send a message, or select Join voice on both devices. This chat is available for the current session.")
            }
        });
        var messages = new StackPanel { Spacing = 8, Children = { empty } };
        var status = Label(null);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var input = ComposerField();
        input.MaxLength = 8000;
        var back = NearbyBackButton(() => ExitNearbyAsync(reset: true));
        var newPairing = NearbyButton("New pairing", () => RestartPairingAsync(reset: true), "primary");
        var disconnect = NearbyButton("Disconnect", "quiet");
        var join = NearbyButton("Join voice", async () =>
        {
            if (_session.Voice.IsInCall)
                await _session.Voice.LeaveCallAsync(false);
            await _nearby.JoinVoiceAsync();
        });
        var leave = NearbyButton("Leave voice", _nearby.LeaveVoiceAsync);
        var mute = NearbyButton("Mute", _nearby.ToggleMuteAsync);
        var play = NearbyButton("Enable audio", _nearby.PlayAudioAsync);
        var retry = NearbyButton("Retry connection check", StartWatchingAsync);
        var header = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                HeadingWithBack(Text("Nearby chat", 23, Ink, FontWeight.Bold), back), status,
                Actions(join, leave, mute, play, disconnect, retry, newPairing)
            }
        };
        ScrollViewer? timeline = null;
        async Task SendAsync()
        {
            var text = input.Text?.Trim();
            if (!input.IsEnabled || string.IsNullOrEmpty(text))
                return;
            await _nearby.SendAsync(text);
            if (input.Text?.Trim() == text)
                input.Text = null;
            AddMessage(text, outgoing: true);
            ScrollToLatest(timeline);
        }

        var send = NearbyButton("Send", SendAsync, "primary");
        input.TextChanged += (_, _) => UpdateSend();
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && !args.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                args.Handled = true;
                _ = RunNearbyAsync(SendAsync);
            }
        };
        var composer = Card(new StackPanel { Spacing = 8, Children = { input, send } }, 10);
        timeline = ShowConversation(header, messages, composer);
        BindNearbyAction(disconnect, async () =>
        {
            await _nearby.ResetAsync();
            UpdateConversation(null);
        });
        WatchNearby(retry, UpdateConversation);
        UpdateConversation(initialState);

        void UpdateSend() => send.IsEnabled = input.IsEnabled && !string.IsNullOrWhiteSpace(input.Text);

        void UpdateConversation(NearbySnapshot? state)
        {
            foreach (var message in state?.Messages ?? [])
                AddMessage(message, outgoing: false);
            if (state?.Messages.Length > 0)
                ScrollToLatest(timeline);

            var connected = state?.Connected == true;
            var ended = state is null || state.Closed;
            var inVoice = state?.InVoice == true;
            join.IsVisible = connected && !inVoice;
            leave.IsVisible = mute.IsVisible = connected && inVoice;
            play.IsVisible = connected && state?.PlaybackBlocked == true;
            mute.Content = state?.Muted == true ? "Unmute" : "Mute";
            input.IsEnabled = connected;
            UpdateSend();
            composer.IsVisible = !ended;
            disconnect.IsVisible = !ended;
            newPairing.IsVisible = ended;
            back.IsVisible = ended;
            status.Text = ended ? state?.Reason ?? "The Nearby connection ended."
                : !connected ? "Reconnecting to the other device…"
                : play.IsVisible ? "Select Enable audio to hear the other person."
                : inVoice && state?.PeerVoice == true ? "Voice connected · End-to-end encrypted"
                : inVoice ? "Waiting for the other person to join voice."
                : state?.PeerVoice == true ? "The other person joined voice."
                : "Connected · End-to-end encrypted";
            if (ended)
            {
                empty.IsVisible = false;
                retry.IsVisible = false;
                if (state?.HasMoreMessages != true)
                {
                    _nearbyTimer.Stop();
                    _refreshNearby = null;
                }
            }
        }

        void AddMessage(string text, bool outgoing)
        {
            var body = new StackPanel { Spacing = 5,
                Children = { Text(outgoing ? "You" : "Nearby device", 12, Ink, FontWeight.SemiBold),
                    RenderMessage(text, Ink, includeChannelTokens: false) } };
            var bubble = new Border
            {
                Background = outgoing ? SenderSurface : Surface, BorderBrush = outgoing ? SenderLine : Line,
                BorderThickness = new Thickness(1), MaxWidth = 650, Padding = new Thickness(12, 9), Child = body,
                HorizontalAlignment = outgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                CornerRadius = outgoing ? new CornerRadius(13, 13, 4, 13) : new CornerRadius(4, 13, 13, 13)
            };
            empty.IsVisible = false;
            if (messages.Children.Count > 500)
                messages.Children.RemoveAt(1);
            messages.Children.Add(bubble);
        }
    }

    private Button NearbyBackButton(Func<Task> action) =>
        BackButton(_session.User is null ? "sign in" : "workspace", () => RunNearbyAsync(action), serialize: false);

    private async Task ExitNearbyAsync(bool reset)
    {
        try
        {
            if (reset)
                await _nearby.ResetAsync();
        }
        finally
        {
            _nearbyTimer.Stop();
            _refreshNearby = null;
            _nearbyOpen = false;
            Notify(string.Empty);
            if (_session.User is null)
                ShowAuthentication();
            else
                await OpenWorkspaceAsync();
        }
    }

    private async Task RestartPairingAsync(bool reset)
    {
        if (reset)
            await _nearby.ResetAsync();
        _nearbyTimer.Stop();
        ShowNearbyPairing();
    }

    private async Task StartWatchingAsync()
    {
        if (_refreshNearby is not { } refresh)
            return;
        _nearbyTimer.Start();
        await refresh();
    }

    private void WatchNearby(Button retry, Action<NearbySnapshot?> update)
    {
        retry.IsVisible = false;
        _refreshNearby = async () =>
        {
            try
            {
                var state = await _nearby.PollAsync();
                retry.IsVisible = false;
                update(state);
            }
            catch (Exception ex)
            {
                _nearbyTimer.Stop();
                retry.IsVisible = true;
                SetNotice(ex.Message, nearby: true);
            }
        };
    }

    private Button NearbyButton(string label, Func<Task> action, string style = "quiet") =>
        StyledButton(label, () => RunNearbyAsync(action), style, serialize: false);

    private static Button NearbyButton(string label, string style) =>
        new() { Content = label, Classes = { "workspace-action", style } };

    private void BindNearbyAction(Button button, Func<Task> action) =>
        button.Click += async (_, _) => await RunNearbyAsync(action);

    private async Task RunNearbyAsync(Func<Task> action)
    {
        if (_nearbyBusy)
            return;
        _nearbyBusy = true;
        try
        {
            if (_nearbyPoll is { } poll)
                await poll;
            SetNotice(string.Empty, nearby: true);
            await action();
        }
        catch (Exception ex)
        {
            SetNotice(ex.Message, nearby: _nearbyOpen);
        }
        finally
        {
            _nearbyBusy = false;
        }
    }

    private void PollNearby()
    {
        if (_nearbyBusy || _nearbyPoll is { IsCompleted: false } || _refreshNearby is not { } refresh)
            return;
        _nearbyPoll = refresh();
    }
}
