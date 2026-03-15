using jihadkhawaja.chat.shared.Models;
using Microsoft.JSInterop;
using MudBlazor;

namespace Egroo.UI.Components.View;

public partial class ChannelView
{
    private sealed class AudioProcessingState
    {
        public bool IsNoiseSuppressionSupported { get; set; }
        public bool IsNoiseSuppressionEnabled { get; set; }
        public bool IsEchoCancellationSupported { get; set; }
        public bool IsEchoCancellationEnabled { get; set; }
        public bool IsAutoGainControlSupported { get; set; }
        public bool IsAutoGainControlEnabled { get; set; }
        public bool WasApplied { get; set; }
    }

    private async Task OnCallButtonClicked()
    {
        var channelId = GetChannelId();
        if (VoiceCallSession.IsInCall && !VoiceCallSession.IsCurrentChannel(channelId))
        {
            Snackbar.Add("Leave your current voice call before joining another channel.", Severity.Warning);
            return;
        }

        if (IsInCall)
        {
            await LeaveCall();
        }
        else
        {
            await JoinCall();
        }
    }

    private async Task JoinCall()
    {
        var channelId = GetChannelId();
        if (channelId == Guid.Empty)
        {
            return;
        }

        bool joined = await VoiceCallSession.JoinCallAsync(channelId, Title);
        if (joined)
        {
            SyncCallStateFromService();
            await InvokeAsync(StateHasChanged);
        }
    }

    private string GetCallButtonTooltip()
    {
        var channelId = GetChannelId();
        if (IsInCall)
        {
            return "Leave Voice Call";
        }

        if (VoiceCallSession.IsInCall && !VoiceCallSession.IsCurrentChannel(channelId))
        {
            return "Already in another voice call";
        }

        return "Join Voice Call";
    }

    private async Task LeaveCall()
    {
        await VoiceCallSession.LeaveCallAsync();
        SyncCallStateFromService();
        await RefreshCallParticipants();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleMute()
    {
        await VoiceCallSession.ToggleMuteAsync();
        SyncCallStateFromService();
        await InvokeAsync(StateHasChanged);
    }

    private async Task ToggleNoiseSuppression(bool enabled)
    {
        await VoiceCallSession.UpdateAudioProcessingSettingAsync("noiseSuppression", enabled, "Noise suppression");
        SyncCallStateFromService();
    }

    private async Task ToggleEchoCancellation(bool enabled)
    {
        await VoiceCallSession.UpdateAudioProcessingSettingAsync("echoCancellation", enabled, "Echo cancellation");
        SyncCallStateFromService();
    }

    private async Task ToggleAutoGainControl(bool enabled)
    {
        await VoiceCallSession.UpdateAudioProcessingSettingAsync("autoGainControl", enabled, "Auto gain control");
        SyncCallStateFromService();
    }

    private async Task UpdateAudioProcessingSetting(string settingName, bool enabled, string displayName)
    {
        var state = await JSRuntime.InvokeAsync<AudioProcessingState>("webrtcInterop.setAudioProcessingSetting", new object?[] { settingName, enabled });
        ApplyAudioProcessingState(state);

        if (state?.WasApplied == false)
        {
            Snackbar.Add($"{displayName} could not be changed on this browser/device.", Severity.Warning);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task SyncAudioProcessingState()
    {
        var state = await JSRuntime.InvokeAsync<AudioProcessingState>("webrtcInterop.getAudioProcessingState", Array.Empty<object?>());
        ApplyAudioProcessingState(state);
    }

    private void ApplyAudioProcessingState(AudioProcessingState? state)
    {
        IsNoiseSuppressionSupported = state?.IsNoiseSuppressionSupported ?? false;
        IsNoiseSuppressionEnabled = state?.IsNoiseSuppressionEnabled ?? IsNoiseSuppressionEnabled;
        IsEchoCancellationSupported = state?.IsEchoCancellationSupported ?? false;
        IsEchoCancellationEnabled = state?.IsEchoCancellationEnabled ?? IsEchoCancellationEnabled;
        IsAutoGainControlSupported = state?.IsAutoGainControlSupported ?? false;
        IsAutoGainControlEnabled = state?.IsAutoGainControlEnabled ?? IsAutoGainControlEnabled;
    }

    private async Task RefreshCallParticipants()
    {
        if (VoiceCallSession.IsCurrentChannel(GetChannelId()))
        {
            SyncCallStateFromService();
            return;
        }

        try
        {
            var participants = await ChatCallService.GetChannelCallParticipants(GetChannelId());
            if (participants != null)
            {
                CallParticipantIds = new HashSet<Guid>(participants);
                await FetchParticipantUsers(participants);
            }
        }
        catch
        {
        }
    }

    private void SyncCallStateFromService()
    {
        var channelId = GetChannelId();
        bool isCurrentChannelCall = VoiceCallSession.IsCurrentChannel(channelId);

        IsInCall = isCurrentChannelCall;
        IsMuted = isCurrentChannelCall && VoiceCallSession.IsMuted;
        IsNoiseSuppressionEnabled = VoiceCallSession.IsNoiseSuppressionEnabled;
        IsNoiseSuppressionSupported = VoiceCallSession.IsNoiseSuppressionSupported;
        IsEchoCancellationEnabled = VoiceCallSession.IsEchoCancellationEnabled;
        IsEchoCancellationSupported = VoiceCallSession.IsEchoCancellationSupported;
        IsAutoGainControlEnabled = VoiceCallSession.IsAutoGainControlEnabled;
        IsAutoGainControlSupported = VoiceCallSession.IsAutoGainControlSupported;
        CallDuration = isCurrentChannelCall ? VoiceCallSession.CallDuration : null;

        if (isCurrentChannelCall)
        {
            CallParticipantIds = new HashSet<Guid>(VoiceCallSession.ParticipantIds);
            CallParticipantUsers = VoiceCallSession.ParticipantUsers.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    private void HandleVoiceCallSessionChanged()
    {
        _ = InvokeAsync(async () =>
        {
            if (_disposed)
            {
                return;
            }

            SyncCallStateFromService();
            await RefreshCallParticipants();
            StateHasChanged();
        });
    }

    private void RegisterCallParticipantBroadcast()
    {
        if (_callParticipantBroadcastRegistered)
        {
            return;
        }

        _onChannelCallParticipantsChanged = async (channelId, participantIds) =>
        {
            if (_disposed || channelId != GetChannelId())
            {
                return;
            }

            if (VoiceCallSession.IsCurrentChannel(channelId))
            {
                SyncCallStateFromService();
                await InvokeAsync(StateHasChanged);
                return;
            }

            CallParticipantIds = new HashSet<Guid>(participantIds);
            await FetchParticipantUsers(participantIds);

            try
            {
                if (!_disposed)
                {
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        };

        ChatCallService.OnChannelCallParticipantsChanged += _onChannelCallParticipantsChanged;
        _callParticipantBroadcastRegistered = true;
    }

    private async Task FetchParticipantUsers(IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds)
        {
            if (!CallParticipantUsers.ContainsKey(userId))
            {
                await FetchAndCacheParticipant(userId);
            }
        }

        var staleIds = CallParticipantUsers.Keys.Except(CallParticipantIds).ToList();
        foreach (var id in staleIds)
        {
            CallParticipantUsers.Remove(id);
        }
    }

    private async Task FetchAndCacheParticipant(Guid userId)
    {
        try
        {
            var user = await ChatUserService.GetUserPublicDetails(userId);
            if (user != null)
            {
                var avatar = await ChatUserService.GetAvatar(userId);
                if (avatar != null)
                {
                    user.AvatarPreview = user.CombineAvatarForPreview(avatar);
                }

                CallParticipantUsers[userId] = user;
            }
        }
        catch
        {
        }
    }
}