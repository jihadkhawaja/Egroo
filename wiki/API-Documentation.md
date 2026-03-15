# API Documentation

This guide provides accurate documentation for the Egroo API, including the REST authentication endpoints and the SignalR real-time hub.

## 📖 API Overview

The Egroo server exposes:
- REST endpoints for authentication under `/api/v1/Auth`
- REST endpoints for agents under `/api/v1/Agent`
- 1 SignalR hub at `/chathub` for real-time user, friend, channel, message, and call management
- Swagger UI in development at `http://localhost:5175/swagger`

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
| `UpdateEncryptionKey(string? publicKey, string? keyId)` | Required | `bool` | Legacy-compatible way to publish or replace the current device public key. Internally this routes through device-key management. |
| `AddEncryptionKey(string publicKey, string keyId, string? deviceLabel)` | Required | `bool` | Register a device key for end-to-end encrypted delivery |
| `RemoveEncryptionKey(string keyId)` | Required | `bool` | Soft-delete one registered device key |
| `GetEncryptionKeys()` | Required | `UserEncryptionKeyInfo[]?` | List active encryption keys for the authenticated user |
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

`Message.Content` is not stored in the `Messages` table. For encrypted delivery flows, the server carries recipient-specific ciphertext in pending-message tables until delivery and the recipient device decrypts locally with its private key.

| Method | Auth | Returns | Description |
|--------|------|---------|-------------|
| `SendMessage(Message message)` | Required | `bool` | Send a message to a channel |
| `UpdateMessage(Message message)` | Required | `bool` | Edit an existing message |
| `SendPendingMessages()` | Required | void | Deliver queued offline messages |
| `UpdatePendingMessage(Guid messageid)` | Required | void | Mark a pending message as received |
| `StartTyping(Guid channelId)` | Required | void | Notify other channel members that the current user started typing |
| `StopTyping(Guid channelId)` | Required | void | Notify other channel members that the current user stopped typing |

Notes:

- a `Message` can carry plain `Content` or recipient-specific `RecipientContents`
- encrypted clients typically publish a device key before participating in encrypted delivery
- multi-device users can register more than one key; the server can emit a v2 envelope that contains multiple wrapped AES keys for the same ciphertext
- `SendPendingMessages()` replays undelivered ciphertext for the authenticated user after reconnect
- `UpdatePendingMessage(messageId)` deletes the authenticated user's pending ciphertext once the device has received and processed it
- agent replies can also include encrypted `AgentRecipientContents` for agent-side processing

### 🔐 End-to-end encryption flow

Egroo uses hybrid encryption for message transport:

1. A sender-side encryption pipeline generates a fresh random AES key and IV for the message plaintext.
2. The plaintext is encrypted with AES-GCM.
3. The AES key is wrapped with RSA-OAEP-SHA256 for each recipient public key.
4. The resulting envelope is attached to `RecipientContents` or `AgentRecipientContents`.
5. The server stores ciphertext in pending-message tables until each recipient acknowledges delivery.
6. The recipient device decrypts the AES key with its local private key and then decrypts the message body locally.

Practical notes:

- v1 payloads contain one wrapped key for one recipient key
- v2 payloads contain multiple wrapped keys for one ciphertext so a user can decrypt on any registered device
- if a device loses its private key, that device can no longer decrypt old ciphertext sent to that key
- plaintext can still appear in memory on the sender or recipient client, but encrypted transport content is what the server persists for delivery

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

### 📞 Voice Configuration (REST)

The browser loads ICE server settings from the API before joining a channel call.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/Voice/config` | Anonymous | Returns normalized ICE server configuration for WebRTC. The server tries Cloudflare TURN credentials first, then configured `VoiceCall:IceServers`, then a default STUN server. |

### 📞 Channel Voice Call Methods (SignalR)

The SignalR hub acts as the membership and signaling plane for channel voice calls. Clients exchange WebRTC offers, answers, and ICE candidates through the hub after joining the same channel call.

| Method | Auth | Description |
|--------|------|-------------|
| `JoinChannelCall(Guid channelId)` | Required | Join a channel voice call. If the call is not active yet, the first participant creates it. |
| `LeaveChannelCall(Guid channelId)` | Required | Leave the current channel voice call and notify remaining participants. |
| `GetChannelCallParticipants(Guid channelId)` | Required | Get the current participant user IDs for a channel voice call. |
| `SendOfferToUser(Guid channelId, Guid targetUserId, string offerSdp)` | Required | Send an SDP offer to a specific participant already in the same channel call. |
| `SendAnswerToUser(Guid channelId, Guid targetUserId, string answerSdp)` | Required | Send an SDP answer to a specific participant already in the same channel call. |
| `SendIceCandidateToUser(Guid channelId, Guid targetUserId, string candidateJson)` | Required | Forward an ICE candidate to a specific participant already in the same channel call. |

Notes:

- `JoinChannelCall` succeeds only when the caller belongs to the channel.
- The joining client receives `ExistingCallParticipants` and is responsible for creating offers to those peers.
- Existing participants receive `UserJoinedCall` when a new user joins.
- `ChannelCallParticipantsChanged` is broadcast to all online channel members, not only the users already in the call.

---

### 📡 Server → Client Events

These events are pushed from the server to connected clients.

| Event | Parameters | Description |
|-------|-----------|-------------|
| `FriendStatusChanged` | `Guid userId, bool isOnline` | A friend came online or went offline |
| `ChannelChange` | `Guid channelId` | Channel was created, modified, or deleted |
| `ReceiveMessage` | `Message message` | A new message was delivered |
| `UpdateMessage` | `Message message` | A message was edited |
| `ExistingCallParticipants` | `Guid channelId, Guid[] participantIds` | Sent to the joining caller with the participants already in the channel call |
| `UserJoinedCall` | `Guid channelId, Guid userId` | Sent to existing call participants when a new user joins |
| `UserLeftCall` | `Guid channelId, Guid userId` | Sent to remaining call participants when a user leaves or disconnects |
| `ChannelCallParticipantsChanged` | `Guid channelId, Guid[] currentParticipants` | The list of participants in a channel call updated |
| `ReceiveOffer` | `Guid channelId, Guid senderId, string offerSdp` | Received WebRTC SDP offer from a peer in a channel call |
| `ReceiveAnswer` | `Guid channelId, Guid senderId, string answerSdp` | Received WebRTC SDP answer from a peer in a channel call |
| `ReceiveIceCandidate` | `Guid channelId, Guid senderId, string candidateJson` | Received ICE candidate from a peer in a channel call |
| `TypingStarted` | `ChannelTypingState typingState` | A user or agent started typing in a channel |
| `TypingStopped` | `ChannelTypingState typingState` | A user or agent stopped typing in a channel |

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

## 🤖 AI Agents (REST)

All agent endpoints are under `/api/v1/Agent` and require a valid JWT unless noted.

### Agent CRUD

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/Agent` | Create a new agent |
| `GET` | `/api/v1/Agent` | List the authenticated user's agents |
| `GET` | `/api/v1/Agent/{agentId}` | Get a single agent |
| `PUT` | `/api/v1/Agent/{agentId}` | Update agent settings |
| `DELETE` | `/api/v1/Agent/{agentId}` | Delete an agent |

### Publishing

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/api/v1/Agent/{agentId}/publish` | Required | Make the agent discoverable by all users |
| `POST` | `/api/v1/Agent/{agentId}/unpublish` | Required | Remove the agent from public discovery |
| `GET` | `/api/v1/Agent/published/search?query=` | Anonymous | Search published agents by name |
| `GET` | `/api/v1/Agent/published/{agentId}` | Anonymous | Get a specific published agent's public details |

### Agent Friends

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/Agent/friends/{agentId}` | Add a published agent as a friend |
| `DELETE` | `/api/v1/Agent/friends/{agentId}` | Remove an agent friend |
| `GET` | `/api/v1/Agent/friends` | List all agent friends |
| `GET` | `/api/v1/Agent/friends/{agentId}/check` | Check if an agent is already a friend |

### Channel Agents

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/Agent/channel/{channelId}/agents/{agentId}` | Add an agent to a channel (caller must be channel admin and the agent must be owned by the caller or be a published friend) |
| `DELETE` | `/api/v1/Agent/channel/{channelId}/agents/{agentId}` | Remove an agent from a channel |
| `GET` | `/api/v1/Agent/channel/{channelId}/agents` | List all agents in a channel |

### Knowledge, Tools, MCP Servers & Conversations

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/v1/Agent/{agentId}/knowledge` | Add a knowledge item |
| `GET` | `/api/v1/Agent/{agentId}/knowledge` | List knowledge items |
| `PUT` | `/api/v1/Agent/knowledge/{knowledgeId}` | Update a knowledge item |
| `DELETE` | `/api/v1/Agent/knowledge/{knowledgeId}` | Delete a knowledge item |
| `POST` | `/api/v1/Agent/{agentId}/tools` | Add a tool |
| `GET` | `/api/v1/Agent/{agentId}/tools` | List tools |
| `PUT` | `/api/v1/Agent/tools/{toolId}` | Update a tool |
| `DELETE` | `/api/v1/Agent/tools/{toolId}` | Delete a tool |
| `GET` | `/api/v1/Agent/builtin-tools` | List available built-in tool definitions |
| `POST` | `/api/v1/Agent/{agentId}/seed-builtin-tools` | Seed default built-in tools for an agent |
| `POST` | `/api/v1/Agent/{agentId}/mcp-servers` | Connect an MCP server |
| `GET` | `/api/v1/Agent/{agentId}/mcp-servers` | List connected MCP servers |
| `PUT` | `/api/v1/Agent/mcp-servers/{serverId}` | Update an MCP server |
| `DELETE` | `/api/v1/Agent/mcp-servers/{serverId}` | Remove an MCP server |
| `POST` | `/api/v1/Agent/mcp-servers/{serverId}/discover` | Discover and sync tools from the MCP server |
| `POST` | `/api/v1/Agent/{agentId}/conversations` | Create a conversation |
| `GET` | `/api/v1/Agent/{agentId}/conversations` | List conversations |
| `DELETE` | `/api/v1/Agent/conversations/{conversationId}` | Delete a conversation |
| `GET` | `/api/v1/Agent/conversations/{conversationId}/messages` | Get messages in a conversation |
| `POST` | `/api/v1/Agent/conversations/{conversationId}/chat` | Send a message (non-streaming) |
| `POST` | `/api/v1/Agent/conversations/{conversationId}/chat/stream` | Send a message (SSE streaming) |

**Example — add an agent to a channel:**
```http
POST /api/v1/Agent/channel/{channelId}/agents/{agentId}
Authorization: Bearer <jwt>
```

**Example — search published agents:**
```http
GET /api/v1/Agent/published/search?query=assistant
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
> `agentDefinitionId` is set on messages produced by an AI agent.

```json
{
  "id": "guid",
  "senderId": "guid",
  "channelId": "guid",
  "referenceId": "guid",
  "agentDefinitionId": "guid or null",
  "dateSent": "2024-01-01T00:00:00Z or null",
  "dateSeen": "2024-01-01T00:00:00Z or null",
  "displayName": "sender display name (not persisted)",
  "content": "message text (not persisted)"
}
```

### `AgentDefinition`
```json
{
  "id": "guid",
  "userId": "guid",
  "name": "My Assistant",
  "description": "string or null",
  "instructions": "You are a helpful assistant...",
  "provider": "OpenAI",
  "model": "gpt-4o",
  "isActive": true,
  "isPublished": false,
  "temperature": 0.7,
  "maxTokens": 2048
}
```

### `UserAgentFriend`
```json
{
  "id": "guid",
  "userId": "guid",
  "agentDefinitionId": "guid"
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
