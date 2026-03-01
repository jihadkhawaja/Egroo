# API Documentation

This guide provides accurate documentation for the Egroo API, including the REST authentication endpoints and the SignalR real-time hub.

## 📖 API Overview

The Egroo server exposes:
- **4 REST endpoints** for authentication (`/api/v1/Auth`)
- **1 SignalR Hub** at `/chathub` for all real-time functionality (user, channel, message, and call management)
- **Swagger UI** (development only): `http://localhost:5175/swagger`

All real-time features — user management, friends, channels, messages, and WebRTC calls — are handled exclusively over SignalR using WebSockets.

**Base URL (development)**: `http://localhost:5175`  
**Base URL (production)**: `https://api.egroo.org` (or your own hosted URL)

## 🔐 Authentication (REST)

### JWT Authentication

Egroo uses JWT (JSON Web Tokens) for authentication. After signing in, include the token in the `Authorization` header for protected endpoints:

```
Authorization: Bearer <your-jwt-token>
```

For SignalR, pass the token via the `access_token` query string parameter (handled automatically by the client library).

---

### Sign Up

**POST** `/api/v1/Auth/signup`  
Access: **Anonymous**  
Rate limit: 100 requests / minute

**Request body:**
```json
{
  "username": "your_username",
  "password": "your_password"
}
```

**Response** (`Operation.Response`):
```json
{
  "success": true,
  "message": "Account created successfully",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "token": null
}
```

---

### Sign In

**POST** `/api/v1/Auth/signin`  
Access: **Anonymous**

**Request body:**
```json
{
  "username": "your_username",
  "password": "your_password"
}
```

**Response** (`Operation.Response`):
```json
{
  "success": true,
  "message": null,
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

---

### Refresh Session

**GET** `/api/v1/Auth/refreshsession`  
Access: **Requires JWT**

Returns a new token to extend the session.

**Response** (`Operation.Response`):
```json
{
  "success": true,
  "message": null,
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

---

### Change Password

**PUT** `/api/v1/Auth/changepassword`  
Access: **Requires JWT**

**Request body:**
```json
{
  "oldPassword": "current_password",
  "newPassword": "new_secure_password"
}
```

**Response** (`Operation.Result`):
```json
{
  "success": true,
  "message": "Password updated successfully"
}
```

---

## ⚡ Real-time Hub (SignalR)

### Connection

The hub endpoint is `/chathub`. It accepts **WebSockets only** (no HTTP fallback transports).

Connect with the JWT token:

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5175/chathub", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult<string?>(jwtToken);
    })
    .Build();

await connection.StartAsync();
```

> After connecting, call `SendPendingMessages` to receive any messages that arrived while offline.

The SignalR hub supports a maximum message size of **10 MB**.

---

### 👤 User Methods

| Method | Auth | Returns | Description |
|--------|------|---------|-------------|
| `GetUserPublicDetails(Guid userId)` | Anonymous | `UserDto?` | Public profile for any user |
| `GetUserPrivateDetails()` | Required | `UserDto?` | Full private profile for self |
| `GetCurrentUserUsername()` | Required | `string?` | Current user's username |
| `IsUsernameAvailable(string username)` | Anonymous | `bool` | Check username availability |
| `UpdateDetails(string? displayname, string? email, string? firstname, string? lastname)` | Required | `bool` | Update profile fields |
| `UpdateAvatar(string? avatarBase64)` | Required | `bool` | Update avatar image |
| `UpdateCover(string? coverBase64)` | Required | `bool` | Update cover image |
| `GetAvatar(Guid userId)` | Anonymous | `MediaResult?` | Get avatar for any user |
| `GetCover(Guid userId)` | Anonymous | `MediaResult?` | Get cover for any user |
| `SearchUser(string query, int maxResult)` | Required | `IEnumerable<UserDto>?` | Search all users |
| `SearchUserFriends(string query, int maxResult)` | Required | `IEnumerable<UserDto>?` | Search own friends |
| `DeleteUser()` | Required | `bool` | Delete the authenticated account |
| `SendFeedback(string text)` | Required | `bool` | Submit feedback |
| `CloseUserSession()` | Required | void | End session and disconnect |

---

### 🤝 Friend Methods

| Method | Auth | Returns | Description |
|--------|------|---------|-------------|
| `AddFriend(string friendusername)` | Required | `bool` | Send friend request |
| `RemoveFriend(string friendusername)` | Required | `bool` | Remove a friend |
| `GetUserFriends(Guid userId)` | Required | `UserFriend[]?` | List all friends |
| `GetUserFriendRequests(Guid userId)` | Required | `UserFriend[]?` | List pending requests |
| `GetUserIsFriend(Guid userId, Guid friendId)` | Required | `bool` | Check friendship status |
| `AcceptFriend(Guid friendId)` | Required | `bool` | Accept a friend request |
| `DenyFriend(Guid friendId)` | Required | `bool` | Deny a friend request |

---

### 📢 Channel Methods

| Method | Auth | Returns | Description |
|--------|------|---------|-------------|
| `CreateChannel(params string[] usernames)` | Required | `Channel?` | Create a new channel |
| `GetUserChannels()` | Required | `Channel[]?` | List joined channels |
| `GetChannel(Guid channelId)` | Required | `Channel?` | Get channel details |
| `GetChannelUsers(Guid channelId)` | Required | `UserDto[]?` | List channel members |
| `AddChannelUsers(Guid channelId, params string[] usernames)` | Required | `bool` | Add members |
| `RemoveChannelUser(Guid channelId, Guid userId)` | Required | `bool` | Remove a member |
| `LeaveChannel(Guid channelId)` | Required | `bool` | Leave a channel |
| `DeleteChannel(Guid channelId)` | Required | `bool` | Delete a channel |
| `ChannelContainUser(Guid channelId, Guid userId)` | Required | `bool` | Check membership |
| `IsChannelAdmin(Guid channelId, Guid userId)` | Required | `bool` | Check admin status |
| `SearchPublicChannels(string searchTerm)` | Required | `Channel[]?` | Search public channels |

---

### 💬 Message Methods

Messages are **end-to-end encrypted at rest**. The `content` field is not stored in the `Messages` table — it lives only in `UserPendingMessages` (encrypted) until delivered.

| Method | Auth | Returns | Description |
|--------|------|---------|-------------|
| `SendMessage(Message message)` | Required | `bool` | Send a message to a channel |
| `UpdateMessage(Message message)` | Required | `bool` | Edit an existing message |
| `SendPendingMessages()` | Required | void | Deliver queued offline messages |
| `UpdatePendingMessage(Guid messageid)` | Required | void | Mark a pending message as received |

**Example — send a message:**
```csharp
var message = new Message
{
    SenderId = currentUserId,
    ChannelId = channelId,
    ReferenceId = Guid.NewGuid(),
    Content = "Hello!"
};
bool ok = await connection.InvokeAsync<bool>("SendMessage", message);
```

---

### 📞 Call Methods (WebRTC)

The SignalR hub acts as a signaling server for peer-to-peer WebRTC voice/video calls.

| Method | Auth | Description |
|--------|------|-------------|
| `CallUser(UserDto targetUser, string sdpOffer)` | Required | Initiate a call with SDP offer |
| `AnswerCall(bool acceptCall, UserDto caller, string sdpAnswer)` | Required | Accept or decline a call |
| `HangUp()` | Required | End the active call |
| `SendSignal(string signal, string targetConnectionId)` | Required | Forward a WebRTC signal |
| `SendIceCandidateToPeer(string candidateJson)` | Required | Forward an ICE candidate |

---

### 📡 Server → Client Events

These events are pushed from the server to connected clients.

| Event | Parameters | Description |
|-------|-----------|-------------|
| `FriendStatusChanged` | `Guid userId, bool isOnline` | A friend came online or went offline |
| `ChannelChange` | `Guid channelId` | Channel was created, modified, or deleted |
| `ReceiveMessage` | `Message message` | A new message was delivered |
| `UpdateMessage` | `Message message` | A message was edited |
| `IncomingCall` | `UserDto caller, string sdpOffer` | Incoming WebRTC call offer |
| `CallDeclined` | `UserDto user, string reason` | Called user declined |
| `CallAccepted` | `UserDto callee, string sdpAnswer` | Called user accepted with SDP answer |
| `CallEnded` | `Guid userId, string reason` | Other party ended the call |
| `ReceiveSignal` | `Guid senderId, string signal` | WebRTC signal forwarded from peer |
| `ReceiveIceCandidate` | `string candidateJson` | ICE candidate forwarded from peer |

**Example listeners (C#):**
```csharp
connection.On<Message>("ReceiveMessage", (message) =>
{
    Console.WriteLine($"New message: {message.Content}");
});

connection.On<Guid, bool>("FriendStatusChanged", (userId, isOnline) =>
{
    Console.WriteLine($"User {userId} is now {(isOnline ? "online" : "offline")}");
});

connection.On<Guid>("ChannelChange", (channelId) =>
{
    // Refresh channel list or details
});
```

---

## 📦 Data Models

### `Operation.Response`
Returned by REST authentication endpoints.
```json
{
  "success": true,
  "message": "string or null",
  "userId": "guid or null",
  "token": "jwt-string or null"
}
```

### `Operation.Result`
Returned by the change password endpoint.
```json
{
  "success": true,
  "message": "string or null"
}
```

### `Channel`
```json
{
  "id": "guid",
  "title": "string or null",
  "isPublic": false,
  "dateCreated": "2024-01-01T00:00:00Z",
  "dateUpdated": "2024-01-01T00:00:00Z or null"
}
```

### `Message`
> `content` and `displayName` are transport-only fields — they are **not stored** in the Messages table.

```json
{
  "id": "guid",
  "senderId": "guid",
  "channelId": "guid",
  "referenceId": "guid",
  "dateSent": "2024-01-01T00:00:00Z or null",
  "dateSeen": "2024-01-01T00:00:00Z or null",
  "displayName": "sender display name (not persisted)",
  "content": "message text (not persisted)"
}
```

### `UserDto`
```json
{
  "id": "guid",
  "username": "string",
  "role": "string",
  "lastLoginDate": "2024-01-01T00:00:00Z or null",
  "isOnline": false,
  "connectionId": "string or null",
  "avatarPreview": "data:image/png;base64,... or null",
  "userDetail": {
    "displayName": "string",
    "firstName": "string",
    "lastName": "string",
    "email": "string"
  }
}
```

### `UserFriend`
```json
{
  "id": "guid",
  "userId": "guid",
  "friendUserId": "guid",
  "dateAcceptedOn": "2024-01-01T00:00:00Z or null"
}
```

### `MediaResult`
```json
{
  "contentType": "png",
  "imageBase64": "base64-encoded image string"
}
```

---

## 🚦 Rate Limiting

All REST endpoints use the `Api` fixed-window rate limiter:

| Limit | Window | Queue Limit |
|-------|--------|-------------|
| 100 requests | 1 minute | 10 requests |

Exceeding the limit returns `429 Too Many Requests`.

---

## 📘 Swagger / OpenAPI

Interactive API documentation is available **in development only** at:

```
http://localhost:5175/swagger
```

Bearer authentication is supported in the Swagger UI — paste your JWT token to test protected endpoints.

---

## 🔧 API Troubleshooting

| Problem | Cause | Solution |
|---------|-------|----------|
| `401 Unauthorized` on REST | Missing or expired JWT | Re-sign-in for a fresh token |
| SignalR connection fails | WebSockets blocked or wrong URL | Verify hub URL and WebSocket support |
| `ReceiveMessage` not firing | Offline when message was sent | Call `SendPendingMessages` after reconnect |
| CORS error | Origin not in allowed list | Add origin to `Api.AllowedOrigins` in appsettings |
| `429 Too Many Requests` | Rate limit exceeded | Back off and retry after 1 minute |

For more help, see the [Troubleshooting Guide](Troubleshooting).
