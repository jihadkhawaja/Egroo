using Egroo.UI.Constants;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace Egroo.UI.Components.View;

public partial class ChannelView
{
    private async Task OnAttachmentFilesChanged(IReadOnlyList<IBrowserFile>? files)
    {
        pendingAttachmentFiles = files ?? Array.Empty<IBrowserFile>();

        IBrowserFile? file = pendingAttachmentFiles.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        if (file.Size > MaxAttachmentBytes)
        {
            Snackbar.Add("Files are limited to 1 MB.", Severity.Error);
            await ClearPendingAttachmentSelectionAsync();
            return;
        }

        if (GetChannelId() == Guid.Empty)
        {
            Snackbar.Add("Unable to determine the current channel.", Severity.Error);
            await ClearPendingAttachmentSelectionAsync();
            return;
        }

        InputDisabled = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await using var stream = file.OpenReadStream(MaxAttachmentBytes);
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            var encryptedFile = await EndToEndEncryptionService.EncryptFileAsync(buffer.ToArray(), file.Name, file.ContentType);
            await using var cipherStream = new MemoryStream(encryptedFile.CipherBytes);
            string uploadName = $"egroo-{Guid.NewGuid():N}.bin";
            var uploaded = await ChannelFileService.Upload(GetChannelId(), cipherStream, uploadName, "application/octet-stream");
            if (uploaded is null)
            {
                Snackbar.Add("Failed to upload file.", Severity.Error);
                await ClearPendingAttachmentSelectionAsync();
                return;
            }

            string absoluteUrl = new Uri(new Uri(Source.ConnectionURL), uploaded.Url).ToString();
            string markdownLink = EndToEndEncryptionService.BuildEncryptedFileToken(absoluteUrl, encryptedFile);
            ChannelAttachmentContent attachmentContent = BuildChannelAttachmentContent(file.Name, file.ContentType, buffer.ToArray(), markdownLink);
            string attachmentSummary = BuildAttachmentSummary(file.Name, file.ContentType, buffer.Length);
            stagedAttachment = new PendingComposerAttachment(
                file.Name,
                BuildAttachmentPreviewText(attachmentSummary),
                attachmentContent.UserMessageContent,
                attachmentContent.AgentMessageContent);

            Snackbar.Add("File attached. Add text if you want, then press send.", Severity.Success);
            await ClearPendingAttachmentSelectionAsync();
            await InvokeAsync(StateHasChanged);
            await inputMudTextField.FocusAsync();
        }
        catch (IOException)
        {
            Snackbar.Add("The selected file could not be read.", Severity.Error);
            await ClearPendingAttachmentSelectionAsync();
        }
        finally
        {
            InputDisabled = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ClearPendingAttachmentSelectionAsync()
    {
        pendingAttachmentFiles = Array.Empty<IBrowserFile>();

        if (attachmentUpload is not null)
        {
            await attachmentUpload.ClearAsync();
        }
        else
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ClearStagedAttachmentAsync()
    {
        stagedAttachment = null;
        await ClearPendingAttachmentSelectionAsync();
        await InvokeAsync(StateHasChanged);
    }

    private static string ComposeMessageContent(string? messageText, string? attachmentContent)
    {
        string trimmedMessage = messageText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(attachmentContent))
        {
            return trimmedMessage;
        }

        return string.IsNullOrWhiteSpace(trimmedMessage)
            ? attachmentContent
            : $"{trimmedMessage}\n\n{attachmentContent}";
    }

    private static string BuildMarkdownLink(string fileName, string url)
    {
        string escapedFileName = fileName.Replace("[", "\\[").Replace("]", "\\]");
        return $"[{escapedFileName}]({url})";
    }

    private static ChannelAttachmentContent BuildChannelAttachmentContent(string fileName, string? contentType, byte[] fileBytes, string encryptedToken)
    {
        string summary = BuildAttachmentSummary(fileName, contentType, fileBytes.LongLength);

        if (IsImageAttachment(contentType))
        {
            string altText = fileName.Replace("[", "\\[").Replace("]", "\\]");
            string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            string dataUri = $"data:{resolvedContentType};base64,{Convert.ToBase64String(fileBytes)}";

            return new ChannelAttachmentContent(
                $"{summary}\n{encryptedToken}",
                $"{summary}\n![{altText}]({dataUri})");
        }

        if (TryReadTextAttachment(fileName, contentType, fileBytes, out string? extractedText))
        {
            return new ChannelAttachmentContent(
                $"<details><summary>{summary}</summary>\n\n~~~text\n{extractedText}\n~~~\n</details>\n{encryptedToken}",
                $"{summary}\n\n~~~text\n{extractedText}\n~~~");
        }

        return new ChannelAttachmentContent($"{summary}\n{encryptedToken}", summary);
    }

    private static string BuildAttachmentSummary(string fileName, string? contentType, long sizeBytes)
    {
        string kind = IsImageAttachment(contentType) ? "Attached image" : "Attached file";
        string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        return $"> {kind}: {fileName} ({resolvedContentType}, {FormatAttachmentSize(sizeBytes)})";
    }

    private static string BuildAttachmentPreviewText(string summary)
    {
        return summary.StartsWith("> ", StringComparison.Ordinal) ? summary[2..] : summary;
    }

    private static bool IsImageAttachment(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadTextAttachment(string fileName, string? contentType, byte[] fileBytes, out string? extractedText)
    {
        extractedText = null;
        if (!IsTextAttachment(fileName, contentType))
        {
            return false;
        }

        try
        {
            string text = System.Text.Encoding.UTF8.GetString(fileBytes);
            if (text.IndexOf('\0') >= 0)
            {
                return false;
            }

            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
            if (text.Length > MaxExtractedAttachmentCharacters)
            {
                text = $"{text[..MaxExtractedAttachmentCharacters]}\n\n[Document truncated after {MaxExtractedAttachmentCharacters} characters.]";
            }

            extractedText = text.Trim();
            return !string.IsNullOrWhiteSpace(extractedText);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTextAttachment(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("yaml", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("csv", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return Path.GetExtension(fileName).ToLowerInvariant() is ".txt" or ".md" or ".markdown" or ".json" or ".xml" or ".csv" or ".log" or ".yml" or ".yaml" or ".html" or ".htm" or ".css" or ".js" or ".ts" or ".tsx" or ".jsx" or ".cs" or ".razor" or ".sql" or ".ps1" or ".sh";
    }

    private static string FormatAttachmentSize(long sizeBytes)
    {
        if (sizeBytes >= 1024 * 1024)
        {
            return $"{sizeBytes / (1024d * 1024d):0.##} MB";
        }

        if (sizeBytes >= 1024)
        {
            return $"{sizeBytes / 1024d:0.##} KB";
        }

        return $"{sizeBytes} B";
    }

    private sealed record ChannelAttachmentContent(string UserMessageContent, string AgentMessageContent);

    private sealed record PendingComposerAttachment(string FileName, string SummaryText, string UserMessageContent, string AgentMessageContent);
}