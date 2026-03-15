using jihadkhawaja.chat.shared.Models;
using Microsoft.JSInterop;
using MudBlazor;
using System.Diagnostics;

namespace Egroo.UI.Components.View;

public partial class AgentChatView
{
    [JSInvokable]
    public async Task HandleComposerEnterKey(bool shiftKey)
    {
        if (shiftKey || isStreaming || isAttachmentBusy || !CanSendMessage)
        {
            return;
        }

        await SendMessage();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SendMessage()
    {
        if (!CanSendMessage || isStreaming || isAttachmentBusy || currentConversation is null)
        {
            return;
        }

        var request = CreateAgentChatRequest(inputMessage, stagedAttachment);
        inputMessage = string.Empty;
        stagedAttachment = null;

        messages.Add(new ChatMessageDisplay { Role = "user", Content = request.DisplayMessage ?? request.Message });
        _needsScrollToBottom = true;
        StateHasChanged();

        isStreaming = true;
        streamingContent.Clear();
        streamCts = new CancellationTokenSource();
        StateHasChanged();

        try
        {
            var renderStopwatch = Stopwatch.StartNew();
            await foreach (var chunk in AgentService.ChatStream(currentConversation.Id, request))
            {
                if (streamCts.IsCancellationRequested)
                {
                    break;
                }

                if (chunk.StartsWith("[ERROR]", StringComparison.Ordinal))
                {
                    Snackbar.Add(chunk, Severity.Error);
                    break;
                }

                streamingContent.Append(chunk);
                if (renderStopwatch.ElapsedMilliseconds < StreamUiThrottleMilliseconds)
                {
                    continue;
                }

                await RefreshStreamingUiAsync();
                renderStopwatch.Restart();
            }

            await RefreshStreamingUiAsync();

            if (streamingContent.Length > 0)
            {
                messages.Add(new ChatMessageDisplay
                {
                    Role = "assistant",
                    Content = streamingContent.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Stream error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isStreaming = false;
            streamingContent.Clear();
            streamCts?.Dispose();
            streamCts = null;
            _needsScrollToBottom = true;
            StateHasChanged();
        }
    }

    private async Task RefreshStreamingUiAsync()
    {
        await InvokeAsync(StateHasChanged);
        await ScrollToBottomIfNearBottom();
    }

    private static AgentChatRequest CreateAgentChatRequest(string? messageText, PendingComposerAttachment? attachment)
    {
        string trimmedMessage = messageText?.Trim() ?? string.Empty;
        if (attachment is null)
        {
            return new AgentChatRequest
            {
                Message = trimmedMessage,
                DisplayMessage = trimmedMessage
            };
        }

        string displayMessage = string.IsNullOrWhiteSpace(trimmedMessage)
            ? attachment.DisplayContent
            : $"{trimmedMessage}\n\n{attachment.DisplayContent}";

        string attachmentRequestContent = attachment.RequestContent;
        string requestMessage = string.IsNullOrWhiteSpace(trimmedMessage)
            ? attachmentRequestContent
            : string.IsNullOrWhiteSpace(attachmentRequestContent)
                ? trimmedMessage
                : $"{trimmedMessage}\n\n{attachmentRequestContent}";

        return new AgentChatRequest
        {
            Message = requestMessage,
            DisplayMessage = displayMessage,
            Attachments = attachment.RequestAttachment is null
                ? null
                : new[] { attachment.RequestAttachment }
        };
    }

    private string GetStreamingRenderKey()
    {
        return $"stream:{currentConversation?.Id}:{streamingContent}";
    }
}