using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using System.Text;

namespace Egroo.UI.Components.View;

public partial class AgentChatView
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

        isAttachmentBusy = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await using var stream = file.OpenReadStream(MaxAttachmentBytes);
            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            stagedAttachment = await BuildAgentAttachmentAsync(file, buffer.ToArray());

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
            isAttachmentBusy = false;
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

    private static async Task<PendingComposerAttachment> BuildAgentAttachmentAsync(IBrowserFile file, byte[] fileBytes)
    {
        string fileName = file.Name;
        string? contentType = file.ContentType;
        string summary = BuildAttachmentSummary(fileName, contentType, fileBytes.LongLength);
        string previewText = BuildAttachmentPreviewText(summary);

        if (IsImageAttachment(contentType))
        {
            string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "image/*" : contentType;
            byte[] previewBytes = await ReadImagePreviewBytesAsync(file, fileBytes);
            string dataUri = BuildDataUri(resolvedContentType, fileBytes);
            string previewDataUri = BuildDataUri(resolvedContentType, previewBytes);
            string displayContent = $"{summary}\n![{EscapeMarkdownLabel(fileName)}]({previewDataUri})";
            return new PendingComposerAttachment(
                fileName,
                previewText,
                displayContent,
                string.Empty,
                new AgentChatAttachment
                {
                    FileName = fileName,
                    ContentType = resolvedContentType,
                    DataUri = dataUri
                });
        }

        if (TryReadTextAttachment(fileName, contentType, fileBytes, MaxExtractedAttachmentCharacters, out string? extractedText))
        {
            string displayExtract = extractedText ?? string.Empty;
            if (TryReadTextAttachment(fileName, contentType, fileBytes, MaxDisplayedAttachmentCharacters, out string? shortenedDisplayExtract)
                && !string.IsNullOrWhiteSpace(shortenedDisplayExtract))
            {
                displayExtract = shortenedDisplayExtract;
            }

            return new PendingComposerAttachment(
                fileName,
                previewText,
                $"<details><summary>{summary}</summary>\n\n~~~text\n{displayExtract}\n~~~\n</details>",
                $"<details><summary>{summary}</summary>\n\n~~~text\n{extractedText}\n~~~\n</details>",
                null);
        }

        return new PendingComposerAttachment(fileName, previewText, summary, summary, null);
    }

    private static async Task<byte[]> ReadImagePreviewBytesAsync(IBrowserFile file, byte[] originalBytes)
    {
        try
        {
            IBrowserFile previewFile = await file.RequestImageFileAsync(file.ContentType, ImagePreviewMaxWidth, ImagePreviewMaxHeight);
            await using var previewStream = previewFile.OpenReadStream(MaxAttachmentBytes);
            await using var previewBuffer = new MemoryStream();
            await previewStream.CopyToAsync(previewBuffer);
            byte[] previewBytes = previewBuffer.ToArray();
            return previewBytes.Length > 0 ? previewBytes : originalBytes;
        }
        catch
        {
            return originalBytes;
        }
    }

    private static string BuildDataUri(string contentType, byte[] fileBytes)
    {
        return $"data:{contentType};base64,{Convert.ToBase64String(fileBytes)}";
    }

    private static string BuildAttachmentPreviewText(string summary)
    {
        return summary.StartsWith("> ", StringComparison.Ordinal) ? summary[2..] : summary;
    }

    private static string BuildAttachmentSummary(string fileName, string? contentType, long sizeBytes)
    {
        string kind = IsImageAttachment(contentType) ? "Attached image" : "Attached file";
        string resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        return $"> {kind}: {fileName} ({resolvedContentType}, {FormatAttachmentSize(sizeBytes)})";
    }

    private static bool IsImageAttachment(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadTextAttachment(string fileName, string? contentType, byte[] fileBytes, int maxCharacters, out string? extractedText)
    {
        extractedText = null;
        if (!IsTextAttachment(fileName, contentType))
        {
            return false;
        }

        try
        {
            string text = Encoding.UTF8.GetString(fileBytes);
            if (text.IndexOf('\0') >= 0)
            {
                return false;
            }

            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
            if (text.Length > maxCharacters)
            {
                text = $"{text[..maxCharacters]}\n\n[Document truncated after {maxCharacters} characters.]";
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

    private static string EscapeMarkdownLabel(string value)
    {
        return value.Replace("[", "\\[").Replace("]", "\\]");
    }
}