using jihadkhawaja.chat.shared.Models;
using MudBlazor;

namespace Egroo.UI.Components.View;

public partial class AgentChatView
{
    private async Task LoadMessages()
    {
        if (currentConversation is null)
        {
            return;
        }

        var dbMessages = await AgentService.GetMessages(currentConversation.Id);
        messages.Clear();

        if (dbMessages is not null)
        {
            foreach (var msg in dbMessages)
            {
                messages.Add(new ChatMessageDisplay
                {
                    Role = msg.Role,
                    Content = msg.Content
                });
            }
        }

        _needsScrollToBottom = true;
        StateHasChanged();
    }

    private async Task CreateNewConversation()
    {
        if (isStreaming)
        {
            Snackbar.Add("Wait for the current response to finish before starting a new conversation.", Severity.Warning);
            return;
        }

        var conv = await AgentService.CreateConversation(agentGuid);
        if (conv is not null)
        {
            currentConversation = conv;
            messages.Clear();
            await RefreshConversations();
            StateHasChanged();
        }
        else
        {
            Snackbar.Add("Failed to create conversation.", Severity.Error);
        }
    }

    private async Task SwitchConversation(AgentConversation conv)
    {
        if (isStreaming || currentConversation?.Id == conv.Id)
        {
            return;
        }

        currentConversation = conv;
        IsBusy = true;
        StateHasChanged();

        await LoadMessages();

        IsBusy = false;
        StateHasChanged();
    }

    private async Task DeleteCurrentConversationConfirm()
    {
        if (currentConversation is null)
        {
            return;
        }

        if (isStreaming)
        {
            Snackbar.Add("Wait for the current response to finish before clearing this conversation.", Severity.Warning);
            return;
        }

        string conversationLabel = currentConversation.Title
            ?? currentConversation.DateCreated?.ToLocalTime().ToString("MMM dd, HH:mm")
            ?? "this conversation";

        bool? confirmed = await DialogService.ShowMessageBoxAsync(
            "Clear Conversation",
            $"Delete \"{conversationLabel}\" and remove its agent chat history?",
            yesText: "Delete",
            cancelText: "Cancel");

        if (confirmed != true)
        {
            return;
        }

        IsBusy = true;
        StateHasChanged();

        try
        {
            bool success = await AgentService.DeleteConversation(currentConversation.Id);
            if (!success)
            {
                Snackbar.Add("Failed to delete conversation.", Severity.Error);
                return;
            }

            Snackbar.Add("Conversation deleted.", Severity.Success);
            messages.Clear();
            currentConversation = null;
            await RefreshConversations();

            if (currentConversation is not null)
            {
                await LoadMessages();
            }
            else
            {
                await CreateNewConversation();
            }
        }
        finally
        {
            IsBusy = false;
            StateHasChanged();
        }
    }

    private async Task RefreshConversations()
    {
        conversations = await AgentService.GetConversations(agentGuid);

        if (conversations is null || conversations.Length == 0)
        {
            conversations = Array.Empty<AgentConversation>();
            currentConversation = null;
            return;
        }

        if (currentConversation is not null)
        {
            currentConversation = conversations.FirstOrDefault(x => x.Id == currentConversation.Id);
        }

        currentConversation ??= conversations.OrderByDescending(c => c.DateCreated).First();
    }
}