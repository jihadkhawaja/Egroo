# API Documentation

This guide provides comprehensive documentation for the Egroo API, including authentication, endpoints, and usage examples.

## 🔗 API Overview

The Egroo API is a RESTful service built with ASP.NET Core that provides:
- **Authentication & Authorization** (JWT-based)
- **Real-time Communication** (SignalR hubs)
- **User Management**
- **Channel Management** 
- **Message Handling**
- **Friend System**

**Base URL**: `http://localhost:5175` (development) or `https://api.yourdomain.com` (production)

## 🔐 Authentication

### JWT Token Authentication

Egroo uses JWT (JSON Web Tokens) for authentication. Include the token in the `Authorization` header:

```
Authorization: Bearer <your-jwt-token>
```

### Login Endpoint

**POST** `/api/auth/login`

```json
{
  "username": "your_username",
  "password": "your_password"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "user-guid",
    "username": "your_username",
    "email": "user@example.com"
  }
}
```

### Registration Endpoint

**POST** `/api/auth/register`

```json
{
  "username": "new_username",
  "email": "user@example.com",
  "password": "secure_password"
}
```

**Response:**
```json
{
  "success": true,
  "message": "User registered successfully",
  "userId": "user-guid"
}
```

## 👤 User Management

### Get Current User

**GET** `/api/users/me`

**Headers:** `Authorization: Bearer <token>`

**Response:**
```json
{
  "id": "user-guid",
  "username": "your_username",
  "email": "user@example.com",
  "createdAt": "2024-01-01T00:00:00Z",
  "isOnline": true
}
```

### Update User Profile

**PUT** `/api/users/me`

**Headers:** `Authorization: Bearer <token>`

```json
{
  "email": "newemail@example.com",
  "displayName": "New Display Name"
}
```

### Search Users

**GET** `/api/users/search?query=username`

**Headers:** `Authorization: Bearer <token>`

**Response:**
```json
[
  {
    "id": "user-guid",
    "username": "found_user",
    "displayName": "Display Name",
    "isOnline": false
  }
]
```

## 👥 Friend System

### Get Friends List

**GET** `/api/friends`

**Headers:** `Authorization: Bearer <token>`

**Response:**
```json
[
  {
    "id": "friend-guid",
    "username": "friend_username",
    "displayName": "Friend Name",
    "isOnline": true,
    "lastSeen": "2024-01-01T12:00:00Z"
  }
]
```

### Send Friend Request

**POST** `/api/friends/request`

**Headers:** `Authorization: Bearer <token>`

```json
{
  "targetUsername": "username_to_add"
}
```

### Accept Friend Request

**POST** `/api/friends/accept/{requestId}`

**Headers:** `Authorization: Bearer <token>`

### Decline Friend Request

**POST** `/api/friends/decline/{requestId}`

**Headers:** `Authorization: Bearer <token>`

## 📢 Channel Management

### Get User Channels

**GET** `/api/channels`

**Headers:** `Authorization: Bearer <token>`

**Response:**
```json
[
  {
    "id": "channel-guid",
    "name": "Channel Name",
    "description": "Channel description",
    "isPrivate": false,
    "memberCount": 5,
    "lastActivity": "2024-01-01T12:00:00Z",
    "unreadCount": 2
  }
]
```

### Create Channel

**POST** `/api/channels`

**Headers:** `Authorization: Bearer <token>`

```json
{
  "name": "New Channel",
  "description": "Channel description",
  "isPrivate": true,
  "initialMembers": ["username1", "username2"]
}
```

### Get Channel Details

**GET** `/api/channels/{channelId}`

**Headers:** `Authorization: Bearer <token>`

**Response:**
```json
{
  "id": "channel-guid",
  "name": "Channel Name",
  "description": "Channel description",
  "isPrivate": false,
  "createdAt": "2024-01-01T00:00:00Z",
  "members": [
    {
      "id": "user-guid",
      "username": "member1",
      "role": "admin",
      "joinedAt": "2024-01-01T00:00:00Z"
    }
  ]
}
```

### Join Channel

**POST** `/api/channels/{channelId}/join`

**Headers:** `Authorization: Bearer <token>`

### Leave Channel

**POST** `/api/channels/{channelId}/leave`

**Headers:** `Authorization: Bearer <token>`

### Add Members to Channel

**POST** `/api/channels/{channelId}/members`

**Headers:** `Authorization: Bearer <token>`

```json
{
  "usernames": ["user1", "user2"]
}
```

## 💬 Message Handling

### Get Channel Messages

**GET** `/api/channels/{channelId}/messages?page=1&limit=50`

**Headers:** `Authorization: Bearer <token>`

**Response:**
```json
{
  "messages": [
    {
      "id": "message-guid",
      "content": "Hello, world!",
      "senderId": "user-guid",
      "senderUsername": "sender_name",
      "timestamp": "2024-01-01T12:00:00Z",
      "messageType": "text",
      "isEdited": false
    }
  ],
  "hasMore": true,
  "totalCount": 150
}
```

### Send Message

**POST** `/api/channels/{channelId}/messages`

**Headers:** `Authorization: Bearer <token>`

```json
{
  "content": "Hello, everyone!",
  "messageType": "text"
}
```

### Edit Message

**PUT** `/api/messages/{messageId}`

**Headers:** `Authorization: Bearer <token>`

```json
{
  "content": "Updated message content"
}
```

### Delete Message

**DELETE** `/api/messages/{messageId}`

**Headers:** `Authorization: Bearer <token>`

## 🔄 Real-time Communication (SignalR)

### Connection

Connect to the SignalR hub at `/chathub` with the JWT token:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chathub", {
        accessTokenFactory: () => localStorage.getItem("authToken")
    })
    .build();
```

### Hub Methods (Client → Server)

#### Join Channel
```javascript
await connection.invoke("JoinChannel", "channel-guid");
```

#### Leave Channel
```javascript
await connection.invoke("LeaveChannel", "channel-guid");
```

#### Send Message
```javascript
await connection.invoke("SendMessage", "channel-guid", "Hello, world!");
```

#### Start Typing
```javascript
await connection.invoke("StartTyping", "channel-guid");
```

#### Stop Typing
```javascript
await connection.invoke("StopTyping", "channel-guid");
```

### Hub Events (Server → Client)

#### Receive Message
```javascript
connection.on("ReceiveMessage", (channelId, message) => {
    console.log("New message:", message);
});
```

#### User Joined Channel
```javascript
connection.on("UserJoinedChannel", (channelId, user) => {
    console.log("User joined:", user);
});
```

#### User Left Channel
```javascript
connection.on("UserLeftChannel", (channelId, userId) => {
    console.log("User left:", userId);
});
```

#### User Typing
```javascript
connection.on("UserTyping", (channelId, username) => {
    console.log(`${username} is typing...`);
});
```

#### User Online Status
```javascript
connection.on("UserOnlineStatusChanged", (userId, isOnline) => {
    console.log(`User ${userId} is ${isOnline ? 'online' : 'offline'}`);
});
```

## 📁 File Upload

### Upload File

**POST** `/api/files/upload`

**Headers:** 
- `Authorization: Bearer <token>`
- `Content-Type: multipart/form-data`

**Body:** Form data with file

**Response:**
```json
{
  "fileId": "file-guid",
  "fileName": "document.pdf",
  "fileSize": 1024000,
  "fileType": "application/pdf",
  "downloadUrl": "/api/files/download/file-guid"
}
```

### Download File

**GET** `/api/files/download/{fileId}`

**Headers:** `Authorization: Bearer <token>`

## 🔍 Search

### Global Search

**GET** `/api/search?query=search_term&type=all`

**Headers:** `Authorization: Bearer <token>`

**Query Parameters:**
- `query`: Search term
- `type`: `users`, `channels`, `messages`, or `all`
- `limit`: Maximum results (default: 20)

**Response:**
```json
{
  "users": [
    {
      "id": "user-guid",
      "username": "found_user",
      "displayName": "Display Name"
    }
  ],
  "channels": [
    {
      "id": "channel-guid",
      "name": "Found Channel",
      "description": "Channel description"
    }
  ],
  "messages": [
    {
      "id": "message-guid",
      "content": "Message containing search term",
      "channelId": "channel-guid",
      "channelName": "Channel Name"
    }
  ]
}
```

## 📊 System Information

### Health Check

**GET** `/health`

**Response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "database": {
      "status": "Healthy",
      "duration": "00:00:00.0089123"
    },
    "signalr": {
      "status": "Healthy",
      "duration": "00:00:00.0001234"
    }
  }
}
```

### API Information

**GET** `/api/system/info`

**Response:**
```json
{
  "version": "1.0.0",
  "environment": "Production",
  "uptime": "2.15:30:25",
  "connections": {
    "signalr": 45,
    "database": 12
  }
}
```

## 📝 Error Handling

### Error Response Format

All API errors follow this format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input data",
    "details": [
      {
        "field": "username",
        "message": "Username is required"
      }
    ]
  },
  "timestamp": "2024-01-01T12:00:00Z",
  "traceId": "trace-guid"
}
```

### Common Error Codes

| Code | Status | Description |
|------|--------|-------------|
| `UNAUTHORIZED` | 401 | Invalid or missing authentication token |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource not found |
| `VALIDATION_ERROR` | 400 | Invalid input data |
| `CONFLICT` | 409 | Resource already exists |
| `RATE_LIMITED` | 429 | Too many requests |
| `INTERNAL_ERROR` | 500 | Server error |

## 🚀 Rate Limiting

API endpoints are rate-limited to prevent abuse:

| Endpoint Category | Limit | Window |
|------------------|-------|--------|
| Authentication | 5 requests | 1 minute |
| General API | 100 requests | 1 minute |
| File Upload | 10 requests | 1 minute |
| Search | 20 requests | 1 minute |

Rate limit headers are included in responses:
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1609459200
```

## 📖 OpenAPI/Swagger Documentation

Interactive API documentation is available at:
- Development: `http://localhost:5175/swagger`
- Production: `https://api.yourdomain.com/swagger`

## 🧪 Testing the API

### Using cURL

```bash
# Login
curl -X POST http://localhost:5175/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "testuser", "password": "password123"}'

# Get channels (with token)
curl -X GET http://localhost:5175/api/channels \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Using JavaScript/Fetch

```javascript
// Login
const loginResponse = await fetch('/api/auth/login', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    username: 'testuser',
    password: 'password123'
  })
});

const loginData = await loginResponse.json();
const token = loginData.token;

// Get channels
const channelsResponse = await fetch('/api/channels', {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});

const channels = await channelsResponse.json();
```

### Using C# HttpClient

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri("http://localhost:5175");

// Login
var loginData = new { username = "testuser", password = "password123" };
var loginJson = JsonSerializer.Serialize(loginData);
var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

var loginResponse = await httpClient.PostAsync("/api/auth/login", loginContent);
var loginResult = await loginResponse.Content.ReadAsStringAsync();
var token = JsonDocument.Parse(loginResult).RootElement.GetProperty("token").GetString();

// Set authorization header
httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", token);

// Get channels
var channelsResponse = await httpClient.GetAsync("/api/channels");
var channels = await channelsResponse.Content.ReadAsStringAsync();
```

## 🔧 SDK and Client Libraries

### JavaScript/TypeScript SDK

```typescript
class EgrooApiClient {
  private baseUrl: string;
  private token?: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  async login(username: string, password: string): Promise<LoginResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });
    
    const data = await response.json();
    this.token = data.token;
    return data;
  }

  async getChannels(): Promise<Channel[]> {
    const response = await fetch(`${this.baseUrl}/api/channels`, {
      headers: { 'Authorization': `Bearer ${this.token}` }
    });
    
    return response.json();
  }
}
```

### C# SDK

```csharp
public class EgrooApiClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public EgrooApiClient(string baseUrl)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var loginData = new { username, password };
        var json = JsonSerializer.Serialize(loginData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/auth/login", content);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        
        _token = result.Token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _token);
            
        return result;
    }

    public async Task<List<Channel>> GetChannelsAsync()
    {
        var response = await _httpClient.GetAsync("/api/channels");
        return await response.Content.ReadFromJsonAsync<List<Channel>>();
    }
}
```

## 🆘 API Troubleshooting

Common API issues and solutions:

### Authentication Issues
- **401 Unauthorized**: Check if JWT token is valid and not expired
- **403 Forbidden**: Verify user has required permissions

### Connection Issues
- **CORS errors**: Ensure client origin is in allowed origins configuration
- **SignalR connection failures**: Check WebSocket support and authentication

### Performance Issues
- **Slow responses**: Check database connection and query performance
- **Rate limiting**: Implement client-side rate limiting and retry logic

For more troubleshooting help, see the [Troubleshooting Guide](Troubleshooting#api-issues).