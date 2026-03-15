using jihadkhawaja.chat.shared.Models;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;

namespace Egroo.UI.Components.View;

public partial class ChannelView
{
    private const int SearchMinimumLength = 2;
    private const int SearchResultLimit = 100;
    private const int SearchWindowRadius = 20;
    private const int DefaultMessageWindowSize = 50;
    private static readonly Regex SearchWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    [JSInvokable]
    public async Task HandleMentionNavigationKey(string key, bool shiftKey)
    {
        if (_disposed)
        {
            return;
        }

        bool mentionPopupActive = showMentionPopup && filteredMentionCandidates.Count > 0;
        if (!mentionPopupActive)
        {
            return;
        }

        if (key is "ArrowDown" or "Down")
        {
            MoveMentionSelection(1);
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (key is "ArrowUp" or "Up")
        {
            MoveMentionSelection(-1);
            await InvokeAsync(StateHasChanged);
            return;
        }

        if ((key == "Enter" || key == "NumpadEnter") && !shiftKey)
        {
            SelectCurrentMentionCandidate();
            await InvokeAsync(StateHasChanged);
        }
    }

    private void OnInputContentChanged()
    {
        UpdateMentionPopup(InputContent);
        _ = HandleTypingInputAsync(InputContent);
    }

    private void ToggleSearch()
    {
        if (IsSearchOpen)
        {
            CloseSearch();
            return;
        }

        IsSearchOpen = true;
    }

    private void CloseSearch()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = null;

        IsSearchOpen = false;
        IsSearchLoading = false;
        SearchQuery = string.Empty;
        SearchResults.Clear();
        VisibleSearchMatchReferenceIds.Clear();
        FocusedSearchReferenceId = null;
        RestoreDefaultMessageWindow();
    }

    private async Task OnSearchQueryChangedAsync(string? value)
    {
        SearchQuery = value?.Trim() ?? string.Empty;
        FocusedSearchReferenceId = null;
        await DebounceSearchAsync();
    }

    private async Task DebounceSearchAsync()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var cancellationToken = _searchDebounceCts.Token;

        if (!HasActiveSearch)
        {
            IsSearchLoading = false;
            SearchResults.Clear();
            VisibleSearchMatchReferenceIds.Clear();
            RestoreDefaultMessageWindow();
            await InvokeAsync(StateHasChanged);
            return;
        }

        IsSearchLoading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await Task.Delay(250, cancellationToken);
            await EnsureChannelCacheLoadedAsync();

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RefreshSearchResultsFromCache();
            IsSearchLoading = false;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task EnsureChannelCacheLoadedAsync()
    {
        if (IsChannelCacheLoaded)
        {
            return;
        }

        var cachedMessages = (await CacheDB.Messages
            .Where(nameof(Message.ChannelId))
            .IsEqual(GetChannelId())
            .ToList()) ?? new List<Message>();

        foreach (var message in cachedMessages)
        {
            await PrepareMessageForDisplayAsync(message);
        }

        CachedChannelMessages = cachedMessages
            .Where(x => x.ReferenceId != Guid.Empty)
            .GroupBy(x => x.ReferenceId)
            .Select(x => x.OrderByDescending(GetMessageSortDate).First())
            .OrderBy(GetMessageSortDate)
            .ToList();

        IsChannelCacheLoaded = true;
    }

    private void RefreshSearchResultsFromCache()
    {
        if (!HasActiveSearch)
        {
            SearchResults.Clear();
            VisibleSearchMatchReferenceIds.Clear();
            return;
        }

        var query = SearchQuery;
        var matches = new List<ChannelSearchResult>();

        for (int index = CachedChannelMessages.Count - 1; index >= 0; index--)
        {
            var message = CachedChannelMessages[index];
            string searchableContent = GetDisplayContent(message);
            if (string.IsNullOrWhiteSpace(searchableContent)
                || !searchableContent.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matches.Add(new ChannelSearchResult(
                message.ReferenceId,
                message.DisplayName ?? (IsCurrentUserMessage(message.SenderId) ? "You" : "Unknown user"),
                BuildSearchPreview(searchableContent, query),
                GetDisplayDate(message),
                IsCurrentUserMessage(message.SenderId) && !IsAgentMessage(message),
                IsAgentMessage(message)));

            if (matches.Count >= SearchResultLimit)
            {
                break;
            }
        }

        SearchResults = matches;
        UpdateVisibleSearchMatches();
    }

    private async Task OpenSearchResultAsync(ChannelSearchResult result)
    {
        await EnsureChannelCacheLoadedAsync();

        int index = CachedChannelMessages.FindIndex(x => x.ReferenceId == result.ReferenceId);
        if (index < 0)
        {
            return;
        }

        int start = Math.Max(0, index - SearchWindowRadius);
        int take = Math.Min(CachedChannelMessages.Count - start, (SearchWindowRadius * 2) + 1);

        Messages = CachedChannelMessages.Skip(start).Take(take).ToList();
        FocusedSearchReferenceId = result.ReferenceId;
        UpdateVisibleSearchMatches();
        _scrollToMessageElementId = GetMessageElementId(result.ReferenceId);
        await InvokeAsync(StateHasChanged);
    }

    private void RestoreDefaultMessageWindow()
    {
        if (IsChannelCacheLoaded && CachedChannelMessages.Count > 0)
        {
            Messages = CachedChannelMessages.TakeLast(DefaultMessageWindowSize).ToList();
        }
    }

    private void UpdateVisibleSearchMatches()
    {
        if (!HasActiveSearch)
        {
            VisibleSearchMatchReferenceIds.Clear();
            return;
        }

        VisibleSearchMatchReferenceIds = Messages
            .Where(x => x.ReferenceId != Guid.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(GetDisplayContent(x))
                && GetDisplayContent(x).Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ReferenceId)
            .ToHashSet();
    }

    private string GetSearchStatusText()
    {
        if (IsSearchLoading)
        {
            return "Searching";
        }

        if (!HasActiveSearch)
        {
            return $"Min {SearchMinimumLength} chars";
        }

        return SearchResults.Count >= SearchResultLimit
            ? $"Top {SearchResultLimit} matches"
            : $"{SearchResults.Count} match{(SearchResults.Count == 1 ? string.Empty : "es")}";
    }

    private string GetSearchResultStyle(ChannelSearchResult result)
    {
        string background = FocusedSearchReferenceId == result.ReferenceId
            ? "rgba(242,89,34,0.16)"
            : "rgba(255,255,255,0.02)";

        return $"cursor: pointer; border: 1px solid rgba(255,255,255,0.10); background: {background};";
    }

    private bool ShouldShowSearchHint()
    {
        return string.IsNullOrWhiteSpace(SearchQuery) || SearchQuery.Length < SearchMinimumLength;
    }

    private string GetDisplayDate(Message message)
    {
        DateTimeOffset? createdOn = message.DateCreated?.ToLocalTime();
        if (!createdOn.HasValue)
        {
            return string.Empty;
        }

        if (createdOn.Value.Date == DateTime.Today)
        {
            return createdOn.Value.ToString("h:mm tt");
        }

        if (createdOn.Value.Date >= DateTime.Today.AddDays(-6))
        {
            return createdOn.Value.ToString("ddd, h:mm tt");
        }

        return createdOn.Value.ToString("dd/MM/yyyy h:mm tt");
    }

    private static DateTimeOffset GetMessageSortDate(Message message)
    {
        return message.DateCreated ?? message.DateSent ?? message.DateUpdated ?? DateTimeOffset.MinValue;
    }

    private static string BuildSearchPreview(string content, string query)
    {
        string normalized = SearchWhitespaceRegex.Replace(content, " ").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        int matchIndex = normalized.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return normalized.Length <= 180 ? normalized : $"{normalized[..180]}...";
        }

        int start = Math.Max(0, matchIndex - 60);
        int end = Math.Min(normalized.Length, matchIndex + query.Length + 100);
        string preview = normalized[start..end].Trim();

        if (start > 0)
        {
            preview = $"...{preview}";
        }

        if (end < normalized.Length)
        {
            preview = $"{preview}...";
        }

        return preview;
    }

    private static string GetMessageElementId(Guid referenceId)
    {
        return $"channel-message-{referenceId:N}";
    }

    private void UpdateMentionPopup(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            showMentionPopup = false;
            filteredMentionCandidates.Clear();
            selectedMentionIndex = -1;
            return;
        }

        var lastAt = text.LastIndexOf('@');
        if (lastAt < 0)
        {
            showMentionPopup = false;
            filteredMentionCandidates.Clear();
            selectedMentionIndex = -1;
            return;
        }

        var afterAt = text[(lastAt + 1)..];
        if (afterAt.Contains('>') && afterAt.IndexOf('<') == 0)
        {
            showMentionPopup = false;
            filteredMentionCandidates.Clear();
            selectedMentionIndex = -1;
            return;
        }

        mentionSearchToken = afterAt.TrimStart('<');

        if (!afterAt.StartsWith("<") && mentionSearchToken.Contains(' '))
        {
            showMentionPopup = false;
            filteredMentionCandidates.Clear();
            selectedMentionIndex = -1;
            return;
        }

        var candidates = new List<MentionCandidate>();

        if (channelAgentDefs is not null)
        {
            candidates.AddRange(channelAgentDefs
                .Where(a => string.IsNullOrEmpty(mentionSearchToken)
                    || a.Name.Contains(mentionSearchToken, StringComparison.OrdinalIgnoreCase))
                .Select(a => new MentionCandidate(
                    true,
                    a.Name,
                    $"{a.Provider} - {a.Model}")));
        }

        candidates.AddRange(channelUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.Username))
            .Where(u => string.IsNullOrEmpty(mentionSearchToken)
                || u.Username!.Contains(mentionSearchToken, StringComparison.OrdinalIgnoreCase))
            .Select(u => new MentionCandidate(false, u.Username!, "Channel member")));

        filteredMentionCandidates = candidates
            .GroupBy(x => $"{x.IsAgent}:{x.Name}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderByDescending(x => x.IsAgent)
            .ThenBy(x => x.Name)
            .ToList();

        showMentionPopup = filteredMentionCandidates.Count > 0;
        selectedMentionIndex = showMentionPopup
            ? Math.Clamp(selectedMentionIndex < 0 ? 0 : selectedMentionIndex, 0, filteredMentionCandidates.Count - 1)
            : -1;
    }

    private void SelectMentionCandidate(MentionCandidate candidate)
    {
        if (string.IsNullOrEmpty(InputContent))
        {
            return;
        }

        var lastAt = InputContent.LastIndexOf('@');
        if (lastAt < 0)
        {
            return;
        }

        var before = InputContent[..lastAt];
        var mention = candidate.Name.Contains(' ') ? $"@<{candidate.Name}> " : $"@{candidate.Name} ";
        InputContent = before + mention;

        showMentionPopup = false;
        filteredMentionCandidates.Clear();
        selectedMentionIndex = -1;
        StateHasChanged();
    }

    private void MoveMentionSelection(int direction)
    {
        if (!showMentionPopup || filteredMentionCandidates.Count == 0)
        {
            selectedMentionIndex = -1;
            return;
        }

        if (selectedMentionIndex < 0)
        {
            selectedMentionIndex = 0;
            return;
        }

        selectedMentionIndex = (selectedMentionIndex + direction + filteredMentionCandidates.Count) % filteredMentionCandidates.Count;
    }

    private void SelectCurrentMentionCandidate()
    {
        if (!showMentionPopup || filteredMentionCandidates.Count == 0)
        {
            return;
        }

        int safeIndex = selectedMentionIndex < 0 ? 0 : selectedMentionIndex;
        SelectMentionCandidate(filteredMentionCandidates[safeIndex]);
    }

    private sealed record MentionCandidate(bool IsAgent, string Name, string? Subtitle);

    private sealed record ChannelSearchResult(
        Guid ReferenceId,
        string DisplayName,
        string PreviewText,
        string DateLabel,
        bool IsCurrentUser,
        bool IsAgent);
}