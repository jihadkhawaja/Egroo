using jihadkhawaja.chat.shared.Models;
using MudBlazor;

namespace Egroo.UI.Components.View;

public partial class ChannelView
{
    private async Task SendMessage()
    {
        if (!CanSendMessage)
        {
            return;
        }

        var currentUser = SessionStorage.User;
        if (currentUser is null)
        {
            return;
        }

        if (!await EnsureComposerEncryptionReadyAsync())
        {
            Snackbar.Add(EncryptionWarning ?? "End-to-end encryption is not ready on this device.", Severity.Warning);
            return;
        }

        await StopTypingAsync();
        InputDisabled = true;

        string plainContent = ComposeMessageContent(InputContent, stagedAttachment?.UserMessageContent);
        string agentPlainContent = ComposeMessageContent(InputContent, stagedAttachment?.AgentMessageContent ?? stagedAttachment?.UserMessageContent);

        Message message = new()
        {
            ChannelId = GetChannelId(),
            DisplayName = currentUser.Username,
            SenderId = currentUser.Id,
        };

        try
        {
            var recipients = channelUsers
                .Concat(new[] { CurrentUserProfile })
                .Where(x => x is not null)
                .Cast<UserDto>();

            var encryptedPayload = await EndToEndEncryptionService.EncryptMessageForRecipientsAsync(
                plainContent,
                recipients,
                channelAgentDefs ?? Array.Empty<AgentDefinition>(),
                agentPlainContent);

            message.RecipientContents = encryptedPayload.UserRecipientContents;
            message.AgentRecipientContents = encryptedPayload.AgentRecipientContents;
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Warning);
            InputDisabled = false;
            await FocusComposerAsync();
            return;
        }

        if (await ChatMessageService.SendMessage(message))
        {
            message.Content = message.RecipientContents?.FirstOrDefault(x => x.UserId == currentUser.Id)?.Content;
            message.DecryptedContent = plainContent;
            message.DateSent ??= DateTimeOffset.UtcNow;

            RemoveTypingParticipantForMessage(message);
            MergeVisibleMessage(message);
            UpsertCachedMessage(message);
            RefreshSearchResultsFromCache();
            await CacheDB.Messages.Put(CreateCacheRecord(message), message.ReferenceId);
            _needsScrollToBottom = true;

            InputContent = string.Empty;
            stagedAttachment = null;
            showMentionPopup = false;
            filteredMentionCandidates.Clear();
            selectedMentionIndex = -1;
            await ClearPendingAttachmentSelectionAsync();
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            Snackbar.Add("Failed to send message, please check your connection and try again.", Severity.Error);
        }

        InputDisabled = false;
        await FocusComposerAsync();
    }

    private async Task<bool> EnsureComposerEncryptionReadyAsync()
    {
        try
        {
            await EnsureComposerStateReadyAsync();
            return IsEndToEndReady && CurrentUserProfile is not null;
        }
        catch
        {
            IsEndToEndReady = false;
            EncryptionWarning = "Unable to initialize end-to-end encryption on this device.";
            return false;
        }
    }

    private void RegisterMessagingEvents()
    {
        _onMessageReceived = async message =>
        {
            if (message.ChannelId != GetChannelId())
            {
                return;
            }

            if (Messages.Any(x => x.Id == message.Id || (x.ReferenceId != Guid.Empty && x.ReferenceId == message.ReferenceId)))
            {
                await PrepareMessageForDisplayAsync(message);
                RemoveTypingParticipantForMessage(message);
                MergeVisibleMessage(message);
                UpsertCachedMessage(message);
                RefreshSearchResultsFromCache();
                await CacheDB.Messages.Put(CreateCacheRecord(message), message.ReferenceId);
                await ChatMessageService.UpdatePendingMessage(message.Id);
                if (message.SenderId == SessionStorage.User?.Id)
                {
                    _needsScrollToBottom = true;
                }
                else
                {
                    _needsScrollToBottomIfNearBottom = true;
                }

                await InvokeAsync(StateHasChanged);
                return;
            }

            await PrepareMessageForDisplayAsync(message);

            RemoveTypingParticipantForMessage(message);
            MergeVisibleMessage(message);
            UpsertCachedMessage(message);
            RefreshSearchResultsFromCache();

            if (message.DateSeen is null && message.SenderId != SessionStorage.User?.Id)
            {
                await ChatMessageService.UpdateMessage(message);
            }

            await CacheDB.Messages.Add(CreateCacheRecord(message));
            await ChatMessageService.UpdatePendingMessage(message.Id);
            if (message.SenderId == SessionStorage.User?.Id)
            {
                _needsScrollToBottom = true;
            }
            else
            {
                _needsScrollToBottomIfNearBottom = true;
            }

            await InvokeAsync(StateHasChanged);
        };
        ChatMessageService.OnMessageReceived += _onMessageReceived;

        _onMessageUpdated = async message =>
        {
            if (message.ChannelId != GetChannelId())
            {
                return;
            }

            for (int i = 0; i < Messages.Count; i++)
            {
                if (message.ReferenceId == Messages[i].ReferenceId)
                {
                    Messages[i].Id = message.Id;
                    Messages[i].DateSeen = message.DateSeen;
                    UpsertCachedMessage(Messages[i]);
                    RefreshSearchResultsFromCache();
                    await CacheDB.Messages.Put(CreateCacheRecord(Messages[i]), Messages[i].ReferenceId);
                    await InvokeAsync(StateHasChanged);
                    return;
                }
            }
        };
        ChatMessageService.OnMessageUpdated += _onMessageUpdated;

        _onTypingStarted = async typingState =>
        {
            if (_disposed || typingState.ChannelId != GetChannelId())
            {
                return;
            }

            if (!typingState.IsAgent && typingState.UserId == SessionStorage.User?.Id)
            {
                return;
            }

            TypingParticipants[GetTypingParticipantKey(typingState)] = typingState;
            await InvokeAsync(StateHasChanged);
        };
        ChatMessageService.OnTypingStarted += _onTypingStarted;

        _onTypingStopped = async typingState =>
        {
            if (_disposed || typingState.ChannelId != GetChannelId())
            {
                return;
            }

            TypingParticipants.Remove(GetTypingParticipantKey(typingState));
            await InvokeAsync(StateHasChanged);
        };
        ChatMessageService.OnTypingStopped += _onTypingStopped;
    }

    private async Task HandleTypingInputAsync(string? text)
    {
        if (_disposed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            await StopTypingAsync();
            return;
        }

        if (!_isTyping)
        {
            _isTyping = true;
            try
            {
                await ChatMessageService.StartTyping(GetChannelId());
            }
            catch
            {
                _isTyping = false;
                return;
            }
        }

        _typingDebounceCts?.Cancel();
        _typingDebounceCts?.Dispose();
        _typingDebounceCts = new CancellationTokenSource();
        _ = StopTypingAfterDelayAsync(_typingDebounceCts.Token);
    }

    private async Task StopTypingAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(1500, token);
            await StopTypingAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task StopTypingAsync()
    {
        _typingDebounceCts?.Cancel();
        _typingDebounceCts?.Dispose();
        _typingDebounceCts = null;

        if (!_isTyping)
        {
            return;
        }

        _isTyping = false;

        try
        {
            await ChatMessageService.StopTyping(GetChannelId());
        }
        catch
        {
        }
    }

    private void RemoveTypingParticipantForMessage(Message message)
    {
        if (message.AgentDefinitionId.HasValue && message.AgentDefinitionId != Guid.Empty)
        {
            TypingParticipants.Remove($"agent:{message.AgentDefinitionId.Value}");
            return;
        }

        TypingParticipants.Remove($"user:{message.SenderId}");
    }

    private static string GetTypingParticipantKey(ChannelTypingState typingState)
    {
        return typingState.AgentDefinitionId.HasValue && typingState.AgentDefinitionId != Guid.Empty
            ? $"agent:{typingState.AgentDefinitionId.Value}"
            : $"user:{typingState.UserId}";
    }

    private string GetTypingSummary()
    {
        var typers = TypingParticipants.Values
            .OrderByDescending(x => x.IsAgent)
            .ThenBy(x => x.DisplayName)
            .Select(x => x.DisplayName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToList();

        if (typers.Count == 0)
        {
            return "Someone is typing...";
        }

        if (typers.Count == 1)
        {
            return $"{typers[0]} is typing...";
        }

        if (typers.Count == 2)
        {
            return $"{typers[0]} and {typers[1]} are typing...";
        }

        int remaining = typers.Count - 2;
        return $"{typers[0]}, {typers[1]}, and {remaining} other{(remaining == 1 ? string.Empty : "s")} are typing...";
    }

    private void UpsertCachedMessage(Message message)
    {
        if (!IsChannelCacheLoaded || message.ReferenceId == Guid.Empty)
        {
            return;
        }

        int index = CachedChannelMessages.FindIndex(x => x.ReferenceId == message.ReferenceId);
        if (index >= 0)
        {
            CachedChannelMessages[index] = message;
        }
        else
        {
            CachedChannelMessages.Add(message);
        }

        CachedChannelMessages = CachedChannelMessages
            .OrderBy(GetMessageSortDate)
            .ToList();
    }

    private void MergeVisibleMessage(Message message)
    {
        if (message.ReferenceId != Guid.Empty)
        {
            int referenceIndex = Messages.FindIndex(x => x.ReferenceId == message.ReferenceId);
            if (referenceIndex >= 0)
            {
                Messages[referenceIndex] = message;
                Messages = Messages.OrderBy(GetMessageSortDate).ToList();
                return;
            }
        }

        int idIndex = Messages.FindIndex(x => x.Id == message.Id && x.Id != Guid.Empty);
        if (idIndex >= 0)
        {
            Messages[idIndex] = message;
        }
        else
        {
            Messages.Add(message);
        }

        Messages = DeduplicateMessages(Messages);
    }

    private static List<Message> DeduplicateMessages(IEnumerable<Message> messages)
    {
        return messages
            .GroupBy(GetMessageIdentity)
            .Select(group => group.OrderByDescending(GetMessageSortDate).First())
            .OrderBy(GetMessageSortDate)
            .ToList();
    }

    private static string GetMessageIdentity(Message message)
    {
        if (message.ReferenceId != Guid.Empty)
        {
            return $"ref:{message.ReferenceId:D}";
        }

        return message.Id != Guid.Empty
            ? $"id:{message.Id:D}"
            : $"fallback:{message.SenderId:D}:{message.ChannelId:D}:{GetMessageSortDate(message).UtcTicks}";
    }
}