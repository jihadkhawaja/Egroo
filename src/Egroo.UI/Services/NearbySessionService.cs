namespace Egroo.UI.Services;

internal sealed class NearbySessionService(Func<ClientRuntime> getRuntime)
{
    public Task<string> InviteAsync() => getRuntime().InvokeAsync<string>("nearby.invite");
    public Task<string> ReplyAsync(string invitation) => getRuntime().InvokeAsync<string>("nearby.reply", invitation);
    public Task AcceptAsync(string reply) => getRuntime().InvokeVoidAsync("nearby.accept", reply);
    public Task ConfirmAsync() => getRuntime().InvokeVoidAsync("nearby.confirm");
    public Task SendAsync(string text) => getRuntime().InvokeVoidAsync("nearby.send", text);
    public Task JoinVoiceAsync() => getRuntime().InvokeVoidAsync("nearby.joinVoice");
    public Task LeaveVoiceAsync() => getRuntime().InvokeVoidAsync("nearby.leaveVoice");
    public Task ToggleMuteAsync() => getRuntime().InvokeVoidAsync("nearby.toggleMute");
    public Task PlayAudioAsync() => getRuntime().InvokeVoidAsync("nearby.playAudio");
    public Task ResetAsync() => getRuntime().InvokeVoidAsync("nearby.reset");
    public Task<NearbySnapshot?> PollAsync() => getRuntime().InvokeAsync<NearbySnapshot?>("nearby.poll");
}

internal sealed record NearbySnapshot(bool Connected, bool WasConnected, bool Closed, string? Reason, string? VerificationCode,
    bool Confirmed, bool InVoice, bool Muted, bool PeerVoice, bool PlaybackBlocked, string[] Messages, bool HasMoreMessages);
