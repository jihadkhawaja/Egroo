using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Egroo.Client.Core;
using Egroo.Client.Services;
using Egroo.Contracts.Interfaces;
using Egroo.Contracts.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Egroo.UI.Services;

internal sealed class WorkspaceSession
{
    private readonly string _tokenKey;
    private bool IsLegacyProductionApi => Api.AbsoluteUri == "https://api.egroo.org/";
    private readonly ClientRuntime _runtime;
    private readonly StorageService _storage;
    private readonly SessionStorage _state = new();
    private readonly AuthService _auth;
    private HubConnection? _hub;
    private DateTimeOffset? _expiresAt;
    private long _connectionGeneration;
    private bool _importLegacyHistory;

    public WorkspaceSession(Uri api, ClientRuntime runtime, Action<string> notify)
    {
        Api = api;
        Http = new HttpClient { BaseAddress = api };
        _runtime = runtime;
        _storage = new StorageService(runtime);
        _tokenKey = "egroo.session:" + api.AbsoluteUri;
        _auth = new AuthService(Http);
        Encryption = new EndToEndEncryptionService(runtime, _storage, Users);
        Agents = new AgentService(Http);
        Files = new ChannelFileService(Http);
        Voice = new VoiceCallSessionService(_state, new ChatCallService(), Users, Http, runtime, notify);
    }

    public Uri Api { get; }
    public HttpClient Http { get; }
    public ITwoFactorAuth AccountSecurity => _auth;
    public ChatUserService Users { get; } = new();
    public ChatChannelService Channels { get; } = new();
    public AgentService Agents { get; }
    public ChannelFileService Files { get; }
    public EndToEndEncryptionService Encryption { get; }
    public VoiceCallSessionService Voice { get; private set; }
    public ChatMessageService Messages { get; private set; } = new();
    public UserDto? User => _state.User;
    public bool Connected => _hub?.State == HubConnectionState.Connected;
    public bool HasExpired => _expiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow;
    public long ConnectionGeneration => Interlocked.Read(ref _connectionGeneration);
    public event Action<Message>? Received;
    public event Action<Message>? Updated;
    public event Action<ChannelTypingState, bool>? Typing;
    public event Action? ConnectionChanged;

    public async Task RestoreAsync()
    {
        var token = await _storage.GetFromLocalStorage(_tokenKey);
        var legacy = token is null && IsLegacyProductionApi;
        if (legacy)
            token = await _storage.GetFromLocalStorage("token");
        if (token is null)
            return;
        if (ReadTokenExpiry(token) is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            await SignOutAsync();
            return;
        }

        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var result = await _auth.RefreshSession();
        if (result.Success && result.Token is { } refreshed)
        {
            await ConnectAsync(refreshed);
            if (legacy)
            {
                await _storage.SetLocalStorage("egroo.legacyHistoryOwner", User!.Id.ToString("D"));
                _importLegacyHistory = true;
                await _storage.RemoveFromLocalStorage("token");
            }
        }
        else
            await SignOutAsync();
    }

    public async Task<Operation.Response> SignInAsync(string username, string password, bool register, string? verificationCode)
    {
        var result = register ? await _auth.SignUp(username, password) : await _auth.SignIn(username, password, verificationCode);
        if (result.RequiresTwoFactor)
            return result;

        if (!result.Success || result.Token is not { } token)
            throw new InvalidOperationException(result.Message ?? "Sign-in failed.");

        await ConnectAsync(token);
        return result;
    }

    private async Task ConnectAsync(string token)
    {
        await DisconnectAsync();
        ChatSignalR.Initialize(new Uri(Api, "chathub").AbsoluteUri, token);
        var hub = ChatSignalR.HubConnection!;
        _hub = hub;
        Interlocked.Increment(ref _connectionGeneration);
        Messages = new ChatMessageService();
        Messages.OnMessageReceived += message => { Received?.Invoke(message); return Task.CompletedTask; };
        Messages.OnMessageUpdated += message => { Updated?.Invoke(message); return Task.CompletedTask; };
        Messages.OnTypingStarted += state => { Typing?.Invoke(state, true); return Task.CompletedTask; };
        Messages.OnTypingStopped += state => { Typing?.Invoke(state, false); return Task.CompletedTask; };
        hub.Reconnecting += _ => OnConnectionChanged(hub);
        hub.Reconnected += _ => OnConnectionChanged(hub);
        hub.Closed += _ => OnConnectionChanged(hub);
        try
        {
            Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await hub.StartAsync();
            _state.User = await Users.GetUserPrivateDetails()
                ?? throw new InvalidOperationException("The server did not return your profile.");
            Encryption.UseIdentityScope(Api, _state.User.Id);
            await Encryption.EnsureIdentityAsync(_state.User);
            _state.User = await Users.GetUserPrivateDetails() ?? _state.User;
            await _storage.SetLocalStorage(_tokenKey, token);
            _importLegacyHistory = IsLegacyProductionApi
                && await _storage.GetFromLocalStorage("egroo.legacyHistoryOwner") == _state.User.Id.ToString("D");
            _expiresAt = ReadTokenExpiry(token);
        }
        catch
        {
            await DisconnectAsync();
            _state.User = null;
            throw;
        }
    }

    private Task OnConnectionChanged(HubConnection hub)
    {
        if (ReferenceEquals(_hub, hub))
        {
            Interlocked.Increment(ref _connectionGeneration);
            ConnectionChanged?.Invoke();
        }

        return Task.CompletedTask;
    }

    public Task<Operation.Result> ChangePasswordAsync(string oldPassword, string newPassword) =>
        _auth.ChangePassword(oldPassword, newPassword);

    public async Task SignOutAsync()
    {
        _state.User = null;
        _importLegacyHistory = false;
        Http.DefaultRequestHeaders.Authorization = null;
        try
        {
            await DisconnectAsync();
        }
        finally
        {
            await _storage.RemoveFromLocalStorage(_tokenKey);
            if (IsLegacyProductionApi)
                await _storage.RemoveFromLocalStorage("token");
        }
    }

    private async Task DisconnectAsync()
    {
        _expiresAt = null;
        if (_hub is not { } hub)
            return;
        _hub = null;
        if (Voice.IsInCall)
            await Voice.LeaveCallAsync(false);
        Messages.Dispose();
        await hub.DisposeAsync();
    }

    private static DateTimeOffset? ReadTokenExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        try
        {
            // Use the expiry claim as a local UI hint. The server validates the token.
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '='));
            using var claims = JsonDocument.Parse(bytes);
            if (claims.RootElement.ValueKind != JsonValueKind.Object
                || !claims.RootElement.TryGetProperty("exp", out var expiry))
                return null;

            var timestamp = expiry.ValueKind switch
            {
                JsonValueKind.Number => expiry.GetRawText(),
                JsonValueKind.String => expiry.GetString(),
                _ => null
            };
            return long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private string HistoryKey(Guid user, Guid channel) => $"{Api.AbsoluteUri}{user:N}/{channel:N}";

    public async Task<Message[]> ReadHistoryAsync(Guid channel, int? limit, CancellationToken cancellationToken)
    {
        if (User is not { } user)
            return [];
        var key = HistoryKey(user.Id, channel);
        if (_importLegacyHistory)
            await _runtime.InvokeVoidAsync("history.importLegacy", cancellationToken, key, channel);
        return await _runtime.InvokeAsync<Message[]>("history.get", cancellationToken, key, limit);
    }

    public async Task StoreMessageAsync(Message message, bool updateOnly = false, bool addOnly = false)
    {
        if (User is not { } user)
            return;
        var record = new Message
        {
            Id = message.Id, ReferenceId = message.ReferenceId, ChannelId = message.ChannelId,
            SenderId = message.SenderId, DisplayName = message.DisplayName, Content = message.Content,
            DateSent = message.DateSent, DateSeen = message.DateSeen, AgentDefinitionId = message.AgentDefinitionId,
            DateCreated = message.DateCreated
        };
        await _runtime.InvokeVoidAsync("history.put", HistoryKey(user.Id, message.ChannelId), record, updateOnly, addOnly);
    }

    public async Task<(string Name, byte[] Bytes)> DownloadAsync(string token)
    {
        var metadata = Egroo.UI.Helpers.EncryptedFileTokenParser.Read(token);
        if (!Uri.TryCreate(metadata.Url, UriKind.Absolute, out var url)
            || !string.Equals(url.GetLeftPart(UriPartial.Authority), Api.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase)
            || !url.AbsolutePath.StartsWith("/api/v1/ChannelFiles/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This attachment belongs to a different Egroo API.");

        var encrypted = await Http.GetByteArrayAsync(url);
        var decrypted = await _runtime.InvokeAsync<string>("egrooCrypto.decryptFile",
            new { encryptedBase64 = Convert.ToBase64String(encrypted), keyBase64 = metadata.Key, ivBase64 = metadata.Iv });
        return (Path.GetFileName(metadata.Name.Replace('\\', '/')), Convert.FromBase64String(decrypted));
    }
}
