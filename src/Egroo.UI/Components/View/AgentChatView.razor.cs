using jihadkhawaja.chat.client.Services;
using jihadkhawaja.chat.shared.Models;
using Egroo.UI.Components.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using System.Text;

namespace Egroo.UI.Components.View;

public partial class AgentChatView : ProtectedViewBase
{
    [Parameter]
    public string? AgentId { get; set; }

    [Parameter]
    public string? Title { get; set; }

    private Guid agentGuid;
    private AgentConversation? currentConversation;
    private AgentConversation[]? conversations;
    private List<ChatMessageDisplay> messages = new();
    private MudTextField<string> inputMudTextField { get; set; } = null!;
    private string inputMessage = string.Empty;
    private bool isStreaming;
    private bool isAttachmentBusy;
    private StringBuilder streamingContent = new();
    private CancellationTokenSource? streamCts;
    private MudFileUpload<IReadOnlyList<IBrowserFile>>? attachmentUpload;
    private IReadOnlyList<IBrowserFile> pendingAttachmentFiles = Array.Empty<IBrowserFile>();
    private PendingComposerAttachment? stagedAttachment;
    private DotNetObjectReference<AgentChatView>? dotNetRef;
    private bool _composerKeyboardRegistered;
    private bool _needsScrollToBottom;
    private const long MaxAttachmentBytes = 1 * 1024 * 1024;
    private const int MaxExtractedAttachmentCharacters = 12000;
    private const int MaxDisplayedAttachmentCharacters = 2000;
    private const int ImagePreviewMaxWidth = 320;
    private const int ImagePreviewMaxHeight = 220;
    private const int StreamUiThrottleMilliseconds = 75;
    private bool CanSendMessage => !string.IsNullOrWhiteSpace(inputMessage) || stagedAttachment is not null;

    protected override async Task OnAccessAsync()
    {
        if (!Guid.TryParse(AgentId, out agentGuid))
        {
            NavigationManager.NavigateTo("/agents");
            return;
        }

        dotNetRef ??= DotNetObjectReference.Create(this);

        IsBusy = true;
        StateHasChanged();

        try
        {
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
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load: {ex.Message}", Severity.Error);
        }
        finally
        {
            IsBusy = false;
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsScrollToBottom)
        {
            _needsScrollToBottom = false;
            await ScrollToBottom();
        }

        if (!_composerKeyboardRegistered && !IsBusy && dotNetRef is not null)
        {
            _composerKeyboardRegistered = true;
            await JSRuntime.InvokeVoidAsync(
                "registerEnterToSend",
                ".agent-chat-composer-input input, .agent-chat-composer-input textarea",
                dotNetRef,
                "HandleComposerEnterKey");
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task ScrollToBottom()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("scrollToEnd", "agent-messages-container");
        }
        catch
        {
        }
    }

    private async Task ScrollToBottomIfNearBottom()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("scrollToEndIfNearBottom", "agent-messages-container", 150);
        }
        catch
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        streamCts?.Cancel();
        streamCts?.Dispose();

        try
        {
            await JSRuntime.InvokeVoidAsync("unregisterEnterToSend", ".agent-chat-composer-input input, .agent-chat-composer-input textarea");
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        dotNetRef?.Dispose();
    }

    private sealed class ChatMessageDisplay
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string RenderKey => $"{Id}:{Role}:{Content}";
    }

    private sealed record PendingComposerAttachment(
        string FileName,
        string SummaryText,
        string DisplayContent,
        string RequestContent,
        AgentChatAttachment? RequestAttachment);
}