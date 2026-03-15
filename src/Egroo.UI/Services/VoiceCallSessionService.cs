using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Interfaces;
using jihadkhawaja.chat.shared.Models;
using Microsoft.JSInterop;
using MudBlazor;
using System.Net.Http.Json;

namespace Egroo.UI.Services
{
    public sealed class VoiceCallSessionService : IAsyncDisposable
    {
        private readonly SessionStorage _sessionStorage;
        private readonly ChatCallService _chatCallService;
        private readonly IUser _chatUserService;
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private readonly ISnackbar _snackbar;

        private readonly HashSet<Guid> _participantIds = new();
        private readonly Dictionary<Guid, UserDto> _participantUsers = new();

        private DotNetObjectReference<VoiceCallSessionService>? _dotNetRef;
        private System.Timers.Timer? _callTimer;
        private DateTime? _callStartTime;
        private bool _initialized;
        private bool _iceConfigurationLoaded;
        private bool _disposed;

        private Func<Guid, Guid[], Task>? _onExistingCallParticipants;
        private Func<Guid, Guid, Task>? _onUserJoinedCall;
        private Func<Guid, Guid, Task>? _onUserLeftCall;
        private Func<Guid, Guid, string, Task>? _onReceiveOffer;
        private Func<Guid, Guid, string, Task>? _onReceiveAnswer;
        private Func<Guid, Guid, string, Task>? _onReceiveIceCandidate;
        private Func<Guid, Guid[], Task>? _onChannelCallParticipantsChanged;

        public VoiceCallSessionService(
            SessionStorage sessionStorage,
            ChatCallService chatCallService,
            IUser chatUserService,
            HttpClient httpClient,
            IJSRuntime jsRuntime,
            ISnackbar snackbar)
        {
            _sessionStorage = sessionStorage;
            _chatCallService = chatCallService;
            _chatUserService = chatUserService;
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _snackbar = snackbar;
        }

        public event Action? StateChanged;

        public bool IsInCall { get; private set; }
        public Guid CurrentChannelId { get; private set; }
        public string? CurrentChannelTitle { get; private set; }
        public bool IsMuted { get; private set; }
        public bool IsNoiseSuppressionEnabled { get; private set; } = true;
        public bool IsNoiseSuppressionSupported { get; private set; } = true;
        public bool IsEchoCancellationEnabled { get; private set; } = true;
        public bool IsEchoCancellationSupported { get; private set; } = true;
        public bool IsAutoGainControlEnabled { get; private set; } = true;
        public bool IsAutoGainControlSupported { get; private set; } = true;
        public string? CallDuration { get; private set; }
        public IReadOnlyCollection<Guid> ParticipantIds => _participantIds;
        public IReadOnlyDictionary<Guid, UserDto> ParticipantUsers => _participantUsers;

        public bool HasUnavailableAudioProcessingFeatures =>
            !IsNoiseSuppressionSupported || !IsEchoCancellationSupported || !IsAutoGainControlSupported;

        public async Task EnsureInitializedAsync()
        {
            if (_initialized || _disposed)
            {
                return;
            }

            await ConfigureIceServersAsync();
            _dotNetRef = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("webrtcInterop.registerDotNetObject", _dotNetRef);
            RegisterCallEvents();
            _initialized = true;
            await SyncAudioProcessingStateAsync();
            NotifyStateChanged();
        }

        public bool IsCurrentChannel(Guid channelId) => IsInCall && CurrentChannelId == channelId;

        public void UpdateCallMetadata(Guid channelId, string? channelTitle)
        {
            if (!IsCurrentChannel(channelId))
            {
                return;
            }

            CurrentChannelTitle = channelTitle;
            NotifyStateChanged();
        }

        public string GetCallNavigationUri()
        {
            if (CurrentChannelId == Guid.Empty)
            {
                return "/channels";
            }

            string title = string.IsNullOrWhiteSpace(CurrentChannelTitle) ? "Channel" : CurrentChannelTitle;
            return $"/chat/{CurrentChannelId:D}/{Uri.EscapeDataString(title)}";
        }

        public async Task<bool> JoinCallAsync(Guid channelId, string? channelTitle)
        {
            if (_disposed || channelId == Guid.Empty)
            {
                return false;
            }

            await EnsureInitializedAsync();

            if (IsCurrentChannel(channelId))
            {
                return true;
            }

            if (IsInCall)
            {
                _snackbar.Add("Leave your current voice call before joining another channel.", Severity.Warning);
                return false;
            }

            try
            {
                string selfUserId = _sessionStorage.User?.Id.ToString() ?? string.Empty;
                bool acquired = await _jsRuntime.InvokeAsync<bool>("webrtcInterop.joinCall", channelId.ToString(), selfUserId);
                if (!acquired)
                {
                    _snackbar.Add("Could not access microphone. Please check permissions.", Severity.Error);
                    return false;
                }

                CurrentChannelId = channelId;
                CurrentChannelTitle = channelTitle;
                IsInCall = true;
                IsMuted = false;

                _participantIds.Clear();
                _participantUsers.Clear();

                if (_sessionStorage.User is not null)
                {
                    _participantIds.Add(_sessionStorage.User.Id);
                    await FetchAndCacheParticipantAsync(_sessionStorage.User.Id);
                }

                await SyncAudioProcessingStateAsync();
                StartCallTimer();
                NotifyStateChanged();

                await _chatCallService.JoinChannelCall(channelId);

                _snackbar.Add("Joined voice call", Severity.Success);
                return true;
            }
            catch (Exception ex)
            {
                _snackbar.Add($"Failed to join call: {ex.Message}", Severity.Error);

                try
                {
                    await _jsRuntime.InvokeVoidAsync("webrtcInterop.leaveCall");
                }
                catch { }

                ResetCallState();
                NotifyStateChanged();

                return false;
            }
        }

        public async Task LeaveCallAsync(bool notifyUser = true)
        {
            if (_disposed)
            {
                return;
            }

            Guid channelId = CurrentChannelId;

            try
            {
                if (channelId != Guid.Empty)
                {
                    await _chatCallService.LeaveChannelCall(channelId);
                }
            }
            catch { }

            try
            {
                await _jsRuntime.InvokeVoidAsync("webrtcInterop.leaveCall");
            }
            catch { }

            ResetCallState();
            NotifyStateChanged();

            if (notifyUser)
            {
                _snackbar.Add("Left voice call", Severity.Info);
            }
        }

        public async Task ToggleMuteAsync()
        {
            if (_disposed || !IsInCall)
            {
                return;
            }

            IsMuted = await _jsRuntime.InvokeAsync<bool>("webrtcInterop.toggleMute");
            NotifyStateChanged();
        }

        public async Task UpdateAudioProcessingSettingAsync(string settingName, bool enabled, string displayName)
        {
            if (_disposed)
            {
                return;
            }

            var state = await _jsRuntime.InvokeAsync<AudioProcessingState>("webrtcInterop.setAudioProcessingSetting", settingName, enabled);
            ApplyAudioProcessingState(state);

            if (state?.WasApplied == false)
            {
                _snackbar.Add($"{displayName} could not be changed on this browser/device.", Severity.Warning);
            }

            NotifyStateChanged();
        }

        [JSInvokable]
        public async Task OnIceCandidateGenerated(string peerId, string candidateJson)
        {
            if (_disposed || !IsInCall || !Guid.TryParse(peerId, out Guid targetUserId))
            {
                return;
            }

            try
            {
                await _chatCallService.SendIceCandidateToUser(CurrentChannelId, targetUserId, candidateJson);
            }
            catch { }
        }

        [JSInvokable]
        public async Task OnPeerDisconnected(string peerId)
        {
            if (_disposed || !Guid.TryParse(peerId, out Guid userId))
            {
                return;
            }

            _participantIds.Remove(userId);
            _participantUsers.Remove(userId);

            try
            {
                await _jsRuntime.InvokeVoidAsync("webrtcInterop.removePeer", peerId);
            }
            catch { }

            NotifyStateChanged();
        }

        public async ValueTask DisposeAsync()
        {
            _disposed = true;

            if (IsInCall)
            {
                await LeaveCallAsync(false);
            }

            StopCallTimer();

            if (_onExistingCallParticipants is not null)
                _chatCallService.OnExistingCallParticipants -= _onExistingCallParticipants;
            if (_onUserJoinedCall is not null)
                _chatCallService.OnUserJoinedCall -= _onUserJoinedCall;
            if (_onUserLeftCall is not null)
                _chatCallService.OnUserLeftCall -= _onUserLeftCall;
            if (_onReceiveOffer is not null)
                _chatCallService.OnReceiveOffer -= _onReceiveOffer;
            if (_onReceiveAnswer is not null)
                _chatCallService.OnReceiveAnswer -= _onReceiveAnswer;
            if (_onReceiveIceCandidate is not null)
                _chatCallService.OnReceiveIceCandidate -= _onReceiveIceCandidate;
            if (_onChannelCallParticipantsChanged is not null)
                _chatCallService.OnChannelCallParticipantsChanged -= _onChannelCallParticipantsChanged;

            _dotNetRef?.Dispose();
        }

        private void RegisterCallEvents()
        {
            _onExistingCallParticipants = async (channelId, participantIds) =>
            {
                if (_disposed || !IsCurrentChannel(channelId))
                {
                    return;
                }

                foreach (var participantId in participantIds)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _participantIds.Add(participantId);

                    try
                    {
                        await FetchAndCacheParticipantAsync(participantId);

                        string? offerSdp = await _jsRuntime.InvokeAsync<string?>(
                            "webrtcInterop.createOfferForPeer", participantId.ToString());

                        if (!string.IsNullOrEmpty(offerSdp) && !_disposed)
                        {
                            await _chatCallService.SendOfferToUser(channelId, participantId, offerSdp);
                        }
                    }
                    catch { }
                }

                NotifyStateChanged();
            };
            _chatCallService.OnExistingCallParticipants += _onExistingCallParticipants;

            _onUserJoinedCall = async (channelId, userId) =>
            {
                if (_disposed || !IsCurrentChannel(channelId))
                {
                    return;
                }

                _participantIds.Add(userId);
                await FetchAndCacheParticipantAsync(userId);
                NotifyStateChanged();
            };
            _chatCallService.OnUserJoinedCall += _onUserJoinedCall;

            _onUserLeftCall = async (channelId, userId) =>
            {
                if (_disposed || channelId != CurrentChannelId)
                {
                    return;
                }

                _participantIds.Remove(userId);
                _participantUsers.Remove(userId);

                try
                {
                    await _jsRuntime.InvokeVoidAsync("webrtcInterop.removePeer", userId.ToString());
                }
                catch { }

                NotifyStateChanged();
            };
            _chatCallService.OnUserLeftCall += _onUserLeftCall;

            _onReceiveOffer = async (channelId, fromUserId, offerSdp) =>
            {
                if (_disposed || !IsCurrentChannel(channelId))
                {
                    return;
                }

                try
                {
                    string? answerSdp = await _jsRuntime.InvokeAsync<string?>(
                        "webrtcInterop.handleOfferFromPeer", fromUserId.ToString(), offerSdp);

                    if (!string.IsNullOrEmpty(answerSdp) && !_disposed)
                    {
                        await _chatCallService.SendAnswerToUser(channelId, fromUserId, answerSdp);
                    }
                }
                catch { }
            };
            _chatCallService.OnReceiveOffer += _onReceiveOffer;

            _onReceiveAnswer = async (channelId, fromUserId, answerSdp) =>
            {
                if (_disposed || !IsCurrentChannel(channelId))
                {
                    return;
                }

                try
                {
                    await _jsRuntime.InvokeVoidAsync("webrtcInterop.handleAnswerFromPeer", fromUserId.ToString(), answerSdp);
                }
                catch { }
            };
            _chatCallService.OnReceiveAnswer += _onReceiveAnswer;

            _onReceiveIceCandidate = async (channelId, fromUserId, candidateJson) =>
            {
                if (_disposed || !IsCurrentChannel(channelId))
                {
                    return;
                }

                try
                {
                    await _jsRuntime.InvokeVoidAsync("webrtcInterop.addIceCandidateForPeer", fromUserId.ToString(), candidateJson);
                }
                catch { }
            };
            _chatCallService.OnReceiveIceCandidate += _onReceiveIceCandidate;

            _onChannelCallParticipantsChanged = async (channelId, participantIds) =>
            {
                if (_disposed || channelId != CurrentChannelId)
                {
                    return;
                }

                _participantIds.Clear();
                foreach (var participantId in participantIds)
                {
                    _participantIds.Add(participantId);
                }

                var currentSet = new HashSet<Guid>(participantIds);
                var staleKeys = _participantUsers.Keys.Where(key => !currentSet.Contains(key)).ToList();
                foreach (var staleKey in staleKeys)
                {
                    _participantUsers.Remove(staleKey);
                }

                foreach (var participantId in participantIds)
                {
                    await FetchAndCacheParticipantAsync(participantId);
                }

                NotifyStateChanged();
            };
            _chatCallService.OnChannelCallParticipantsChanged += _onChannelCallParticipantsChanged;
        }

        private async Task FetchAndCacheParticipantAsync(Guid userId)
        {
            try
            {
                var user = await _chatUserService.GetUserPublicDetails(userId);
                if (user is null)
                {
                    return;
                }

                var avatar = await _chatUserService.GetAvatar(userId);
                if (avatar is not null)
                {
                    user.AvatarPreview = user.CombineAvatarForPreview(avatar);
                }

                _participantUsers[userId] = user;
            }
            catch { }
        }

        private async Task ConfigureIceServersAsync()
        {
            if (_iceConfigurationLoaded || _disposed)
            {
                return;
            }

            _iceConfigurationLoaded = true;

            try
            {
                var configuration = await _httpClient.GetFromJsonAsync<VoiceCallConfigurationResponse>("api/v1/Voice/config");
                await _jsRuntime.InvokeVoidAsync("webrtcInterop.configureIceServers", configuration?.IceServers ?? Array.Empty<VoiceCallIceServerResponse>());
            }
            catch
            {
                await _jsRuntime.InvokeVoidAsync("webrtcInterop.configureIceServers", Array.Empty<VoiceCallIceServerResponse>());
            }
        }

        [JSInvokable]
        public async Task OnOfferGenerated(string peerId, string offerSdp)
        {
            if (_disposed || !IsInCall || !Guid.TryParse(peerId, out Guid targetUserId) || string.IsNullOrWhiteSpace(offerSdp))
            {
                return;
            }

            try
            {
                await _chatCallService.SendOfferToUser(CurrentChannelId, targetUserId, offerSdp);
            }
            catch { }
        }

        private async Task SyncAudioProcessingStateAsync()
        {
            var state = await _jsRuntime.InvokeAsync<AudioProcessingState>("webrtcInterop.getAudioProcessingState");
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

        private void StartCallTimer()
        {
            StopCallTimer();

            _callStartTime = DateTime.UtcNow;
            CallDuration = "00:00";
            _callTimer = new System.Timers.Timer(1000);
            _callTimer.Elapsed += (_, _) =>
            {
                if (_callStartTime.HasValue)
                {
                    var elapsed = DateTime.UtcNow - _callStartTime.Value;
                    CallDuration = elapsed.TotalHours >= 1
                        ? elapsed.ToString(@"hh\:mm\:ss")
                        : elapsed.ToString(@"mm\:ss");
                    NotifyStateChanged();
                }
            };
            _callTimer.Start();
        }

        private void StopCallTimer()
        {
            _callTimer?.Stop();
            _callTimer?.Dispose();
            _callTimer = null;
            _callStartTime = null;
            CallDuration = null;
        }

        private void ResetCallState()
        {
            IsInCall = false;
            CurrentChannelId = Guid.Empty;
            CurrentChannelTitle = null;
            IsMuted = false;
            _participantIds.Clear();
            _participantUsers.Clear();
            StopCallTimer();
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

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

        private sealed class VoiceCallConfigurationResponse
        {
            public VoiceCallIceServerResponse[] IceServers { get; set; } = [];
        }

        private sealed class VoiceCallIceServerResponse
        {
            public string[] Urls { get; set; } = [];
            public string? Username { get; set; }
            public string? Credential { get; set; }
        }
    }
}