using BlazorDexie.JsModule;
using Egroo.UI.CacheDB;
using jihadkhawaja.chat.client;
using jihadkhawaja.chat.client.Core;
using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using MudBlazor;

namespace Egroo.UI.Components.View;

public partial class ChannelView
{
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? ChannelId { get; set; }

    private bool IsAdmin { get; set; }
    private List<Message> Messages { get; set; } = new();
    private MudTextField<string> inputMudTextField { get; set; } = null!;
    private string? InputContent { get; set; }
    private int MaxMessageLength { get; set; } = 300;
    private const long MaxAttachmentBytes = 1 * 1024 * 1024;
    private const int MaxExtractedAttachmentCharacters = 12000;
    private bool InputDisabled { get; set; }
    private MudFileUpload<IReadOnlyList<IBrowserFile>>? attachmentUpload;
    private IReadOnlyList<IBrowserFile> pendingAttachmentFiles = Array.Empty<IBrowserFile>();
    private PendingComposerAttachment? stagedAttachment;
    private EgrooDB CacheDB { get; set; } = null!;
    private bool IsEndToEndReady { get; set; }
    private string? EncryptionWarning { get; set; }
    private UserDto? CurrentUserProfile { get; set; }
    private bool CanSendMessage => !string.IsNullOrWhiteSpace(InputContent) || stagedAttachment is not null;

    private AgentDefinition[]? channelAgentDefs;
    private UserDto[] channelUsers = Array.Empty<UserDto>();
    private bool showMentionPopup;
    private List<MentionCandidate> filteredMentionCandidates = new();
    private int selectedMentionIndex = -1;
    private string mentionSearchToken = string.Empty;

    private bool IsInCall { get; set; }
    private bool IsMuted { get; set; }
    private bool IsNoiseSuppressionEnabled { get; set; } = true;
    private bool IsNoiseSuppressionSupported { get; set; } = true;
    private bool IsEchoCancellationEnabled { get; set; } = true;
    private bool IsEchoCancellationSupported { get; set; } = true;
    private bool IsAutoGainControlEnabled { get; set; } = true;
    private bool IsAutoGainControlSupported { get; set; } = true;
    private HashSet<Guid> CallParticipantIds { get; set; } = new();
    private Dictionary<Guid, UserDto> CallParticipantUsers { get; set; } = new();
    private DotNetObjectReference<ChannelView>? dotNetRef;
    private string? CallDuration { get; set; }
    private bool _callParticipantBroadcastRegistered;

    private bool _disposed;
    private bool _mentionKeyboardRegistered;
    private bool _needsScrollToBottom;
    private bool _needsScrollToBottomIfNearBottom;
    private string? _scrollToMessageElementId;
    private bool _isTyping;
    private CancellationTokenSource? _typingDebounceCts;
    private CancellationTokenSource? _searchDebounceCts;
    private Dictionary<string, ChannelTypingState> TypingParticipants { get; set; } = new();
    private List<Message> CachedChannelMessages { get; set; } = new();
    private List<ChannelSearchResult> SearchResults { get; set; } = new();
    private HashSet<Guid> VisibleSearchMatchReferenceIds { get; set; } = new();
    private bool IsSearchOpen { get; set; }
    private bool IsSearchLoading { get; set; }
    private bool IsChannelCacheLoaded { get; set; }
    private string SearchQuery { get; set; } = string.Empty;
    private Guid? FocusedSearchReferenceId { get; set; }
    private Guid _loadedChannelId;

    private Func<Message, Task>? _onMessageReceived;
    private Func<Message, Task>? _onMessageUpdated;
    private Func<ChannelTypingState, Task>? _onTypingStarted;
    private Func<ChannelTypingState, Task>? _onTypingStopped;
    private Func<Guid, Guid[], Task>? _onChannelCallParticipantsChanged;

    protected override async Task OnAccessAsync()
    {
        IsBusy = true;
        try
        {
            RegisterMessagingEvents();
            CacheDB = new EgrooDB(BlazorDexieOptions);
            Messages = new();
            TypingParticipants.Clear();

            dotNetRef?.Dispose();
            dotNetRef = DotNetObjectReference.Create(this);
            VoiceCallSession.StateChanged += HandleVoiceCallSessionChanged;
            await VoiceCallSession.EnsureInitializedAsync();
            RegisterCallParticipantBroadcast();
            SyncCallStateFromService();

            await RefreshCallParticipants();
            await RefreshChannelPeopleAsync(GetChannelId());

            try
            {
                CurrentUserProfile = await ChatUserService.GetUserPrivateDetails() ?? SessionStorage.User;
                await RefreshEncryptionStateAsync();
            }
            catch
            {
                IsEndToEndReady = false;
                EncryptionWarning = "Unable to initialize end-to-end encryption on this device.";
            }
        }
        catch (Exception ex)
        {
            IsBusy = false;
            Snackbar.Add($"Unable to initialize the channel. {ex.Message}", Severity.Warning);
            await InvokeAsync(StateHasChanged);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var channelId = GetChannelId();

        if (channelId != Guid.Empty && channelId != _loadedChannelId)
        {
            await RefreshChannelPeopleAsync(channelId);
        }

        if (channelId != Guid.Empty && SessionStorage?.User != null)
        {
            IsAdmin = await ChatChannelService.IsChannelAdmin(channelId, SessionStorage.User.Id);
        }

        VoiceCallSession.UpdateCallMetadata(channelId, Title);
        SyncCallStateFromService();
        await RefreshCallParticipants();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                if (ChatSignalR.HubConnection?.State == HubConnectionState.Connected)
                {
                    await ChatMessageService.SendPendingMessages();
                }

                var messages = (await CacheDB.Messages
                    .Where(nameof(Message.ChannelId))
                    .IsEqual(GetChannelId())
                    .Limit(50).ToList()) ?? new List<Message>();

                foreach (var message in messages)
                {
                    await PrepareMessageForDisplayAsync(message);
                }

                Messages = DeduplicateMessages(messages);
                _ = UpdateUnseenMessages();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Unable to finish loading the channel. {ex.Message}", Severity.Warning);
            }
            finally
            {
                IsBusy = false;
                await InvokeAsync(StateHasChanged);
            }

            await Task.Yield();
            ScrollToMessagesEnd();
        }

        if (_needsScrollToBottom)
        {
            _needsScrollToBottom = false;
            ScrollToMessagesEnd();
        }
        else if (_needsScrollToBottomIfNearBottom)
        {
            _needsScrollToBottomIfNearBottom = false;
            ScrollToMessagesEndIfNearBottom();
        }

        if (!string.IsNullOrWhiteSpace(_scrollToMessageElementId))
        {
            string targetId = _scrollToMessageElementId;
            _scrollToMessageElementId = null;
            await JSRuntime.InvokeVoidAsync("scrollElementIntoView", targetId);
        }

        if (!_mentionKeyboardRegistered && !IsBusy && dotNetRef is not null)
        {
            _mentionKeyboardRegistered = true;
            await JSRuntime.InvokeVoidAsync("registerMentionNavigation", ".channel-composer-input input, .channel-composer-input textarea", dotNetRef);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task RefreshChannelPeopleAsync(Guid channelId)
    {
        if (channelId == Guid.Empty)
        {
            channelAgentDefs = Array.Empty<AgentDefinition>();
            channelUsers = Array.Empty<UserDto>();
            _loadedChannelId = Guid.Empty;
            UpdateMentionPopup(InputContent);
            return;
        }

        try
        {
            channelAgentDefs = await AgentService.GetChannelAgents(channelId) ?? Array.Empty<AgentDefinition>();
        }
        catch
        {
            channelAgentDefs = Array.Empty<AgentDefinition>();
        }

        try
        {
            channelUsers = (await ChatChannelService.GetChannelUsers(channelId) ?? Array.Empty<UserDto>())
                .Where(x => x.Id != SessionStorage.User?.Id)
                .OrderBy(x => x.Username)
                .ToArray();
        }
        catch
        {
            channelUsers = Array.Empty<UserDto>();
        }

        _loadedChannelId = channelId;
        UpdateMentionPopup(InputContent);
    }

    private Guid GetChannelId()
    {
        return Guid.TryParse(ChannelId, out Guid id) ? id : Guid.Empty;
    }

    private Task HandleInputKeyDown(KeyboardEventArgs e)
    {
        if ((e.Key == "Enter" || e.Key == "NumpadEnter") && !e.ShiftKey)
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    [JSInvokable]
    public async Task HandleComposerEnterKey(bool shiftKey)
    {
        if (_disposed || shiftKey || InputDisabled)
        {
            return;
        }

        await SendMessage();
        await InvokeAsync(StateHasChanged);
    }

    private async Task FocusComposerAsync()
    {
        try
        {
            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            if (!_disposed)
            {
                await inputMudTextField.FocusAsync();
            }
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ScrollToMessagesEnd()
    {
        JSRuntime.InvokeVoidAsync("scrollToEnd", "messages-container");
    }

    private void ScrollToMessagesEndIfNearBottom()
    {
        JSRuntime.InvokeVoidAsync("scrollToEndIfNearBottom", "messages-container", 150);
    }

    private bool IsCurrentUserMessage(Guid senderId) => senderId == SessionStorage.User?.Id;
    private bool IsAgentMessage(Message msg) => msg.AgentDefinitionId.HasValue && msg.AgentDefinitionId != Guid.Empty;
    private bool HasActiveSearch => SearchQuery.Length >= SearchMinimumLength;
    private static string GetDisplayContent(Message message) => message.DecryptedContent ?? message.Content ?? string.Empty;

    private async Task EnsureComposerStateReadyAsync()
    {
        CurrentUserProfile = await ChatUserService.GetUserPrivateDetails() ?? CurrentUserProfile ?? SessionStorage.User;
        await RefreshEncryptionStateAsync();
        await RefreshChannelPeopleAsync(GetChannelId());
    }

    private async Task RefreshEncryptionStateAsync()
    {
        var readiness = await EndToEndEncryptionService.EnsureIdentityAsync(CurrentUserProfile);
        IsEndToEndReady = readiness.IsReady;
        EncryptionWarning = readiness.Message;

        if (!readiness.IsReady || CurrentUserProfile is null || readiness.Identity is null)
        {
            return;
        }

        CurrentUserProfile.EncryptionPublicKey = readiness.Identity.PublicKey;
        CurrentUserProfile.EncryptionKeyId = readiness.Identity.KeyId;
        CurrentUserProfile.EncryptionKeyUpdatedOn = DateTimeOffset.UtcNow;

        if (SessionStorage.User?.Id == CurrentUserProfile.Id)
        {
            SessionStorage.User.EncryptionPublicKey = CurrentUserProfile.EncryptionPublicKey;
            SessionStorage.User.EncryptionKeyId = CurrentUserProfile.EncryptionKeyId;
            SessionStorage.User.EncryptionKeyUpdatedOn = CurrentUserProfile.EncryptionKeyUpdatedOn;
        }
    }

    private async Task PrepareMessageForDisplayAsync(Message message)
    {
        if (message.AgentDefinitionId is null || message.AgentDefinitionId == Guid.Empty)
        {
            var senderUser = await ChatUserService.GetUserPublicDetails(message.SenderId);
            message.DisplayName = senderUser?.Username ?? message.DisplayName;
        }

        await EndToEndEncryptionService.GetDisplayContentAsync(message);
    }

    private static Message CreateCacheRecord(Message source)
    {
        return new Message
        {
            Id = source.Id,
            SenderId = source.SenderId,
            ChannelId = source.ChannelId,
            ReferenceId = source.ReferenceId,
            DateSent = source.DateSent,
            DateSeen = source.DateSeen,
            DateCreated = source.DateCreated,
            DateUpdated = source.DateUpdated,
            DateDeleted = source.DateDeleted,
            AgentDefinitionId = source.AgentDefinitionId,
            DisplayName = source.DisplayName,
            Content = source.Content,
            DecryptedContent = source.DecryptedContent,
            RecipientContents = source.RecipientContents,
            AgentRecipientContents = source.AgentRecipientContents,
        };
    }

    private async Task UpdateUnseenMessages()
    {
        foreach (var msg in Messages)
        {
            if (msg.DateSeen is null && msg.SenderId != SessionStorage.User?.Id)
            {
                await ChatMessageService.UpdateMessage(msg);
            }
        }
    }

    private void EditChannel()
    {
        NavigationManager.NavigateTo($"/channeldetail/{ChannelId}/{Title}");
    }

    public async ValueTask DisposeAsync()
    {
        await StopTypingAsync();
        _disposed = true;
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();

        try
        {
            await JSRuntime.InvokeVoidAsync("unregisterMentionNavigation", ".channel-composer-input input, .channel-composer-input textarea");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        if (_onMessageReceived is not null)
        {
            ChatMessageService.OnMessageReceived -= _onMessageReceived;
        }

        if (_onMessageUpdated is not null)
        {
            ChatMessageService.OnMessageUpdated -= _onMessageUpdated;
        }

        if (_onTypingStarted is not null)
        {
            ChatMessageService.OnTypingStarted -= _onTypingStarted;
        }

        if (_onTypingStopped is not null)
        {
            ChatMessageService.OnTypingStopped -= _onTypingStopped;
        }

        if (_onChannelCallParticipantsChanged is not null)
        {
            ChatCallService.OnChannelCallParticipantsChanged -= _onChannelCallParticipantsChanged;
        }

        VoiceCallSession.StateChanged -= HandleVoiceCallSessionChanged;
        dotNetRef?.Dispose();
    }
}