# Architecture Overview

This document provides a comprehensive overview of Egroo's architecture, design patterns, and technical implementation.

## 🏗️ System Architecture

Egroo follows a modern, distributed architecture designed for scalability and maintainability:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Web Client    │    │   Mobile App    │    │  Desktop App    │
│  (Blazor WASM)  │    │   (Future)      │    │   (Future)      │
└─────────┬───────┘    └─────────┬───────┘    └─────────┬───────┘
          │                      │                      │
          └──────────────────────┼──────────────────────┘
                                 │
                    ┌─────────────┴─────────────┐
                    │      Load Balancer        │
                    │       (Nginx)             │
                    └─────────────┬─────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │    Blazor Server Host     │
                    │   (Auto Mode: SSR + WASM) │
                    └─────────────┬─────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │      API Gateway          │
                    │    (ASP.NET Core)         │
                    └─────────────┬─────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
┌─────────┴─────────┐   ┌─────────┴─────────┐   ┌─────────┴─────────┐
│   Chat Service    │   │   User Service    │   │  Channel Service  │
│   (SignalR Hub)   │   │   (Identity)      │   │  (Management)     │
└─────────┬─────────┘   └─────────┬─────────┘   └─────────┬─────────┘
          │                       │                       │
          └───────────────────────┼───────────────────────┘
                                  │
                    ┌─────────────┴─────────────┐
                    │    PostgreSQL Database    │
                    │    (Primary Storage)      │
                    └───────────────────────────┘
```

## 🎯 Design Principles

### 1. **Blazor Auto Mode**
- **Server-Side Rendering (SSR)**: Fast initial load
- **WebAssembly (WASM)**: Rich client-side interactivity after caching
- **Seamless Transition**: Automatic upgrade from server to client rendering

### 2. **Real-time First**
- **SignalR**: WebSocket-based real-time communication
- **WebRTC**: Peer-to-peer audio/video calls (future)
- **Progressive Enhancement**: Works without JavaScript, enhanced with it

### 3. **Privacy by Design**
- **Ephemeral Messages**: Automatic deletion after delivery
- **Self-hosted**: Complete data ownership
- **End-to-end Encryption**: Client-side encryption (planned)

### 4. **Progressive Web App**
- **Offline Capability**: Service worker for offline messaging
- **App-like Experience**: Installable on mobile and desktop
- **Push Notifications**: Real-time notifications even when closed

## 🧱 Component Architecture

### Frontend Layer

#### Blazor Components Hierarchy
```
App.razor
├── MainLayout.razor
│   ├── NavMenu.razor
│   ├── UserProfile.razor
│   └── NotificationCenter.razor
├── Pages/
│   ├── Chat/
│   │   ├── ChannelView.razor
│   │   ├── MessageList.razor
│   │   └── MessageInput.razor
│   ├── Friends/
│   │   ├── FriendsList.razor
│   │   └── FriendRequests.razor
│   └── Settings/
│       ├── ProfileSettings.razor
│       └── AppSettings.razor
└── Shared/
    ├── Components/
    │   ├── Modal.razor
    │   ├── Toast.razor
    │   └── LoadingSpinner.razor
    └── Services/
        ├── ChatService.cs
        ├── AuthService.cs
        └── StateService.cs
```

#### State Management
```csharp
public class AppState
{
    public User? CurrentUser { get; set; }
    public List<Channel> Channels { get; set; } = new();
    public List<Friend> Friends { get; set; } = new();
    public Dictionary<Guid, List<Message>> ChannelMessages { get; set; } = new();
    
    public event Action? StateChanged;
    
    public void NotifyStateChanged() => StateChanged?.Invoke();
}
```

### Backend Layer

#### API Structure
```
Controllers/
├── AuthController.cs          # Authentication & Authorization
├── UsersController.cs         # User management
├── ChannelsController.cs      # Channel operations
├── MessagesController.cs      # Message handling
├── FriendsController.cs       # Friend system
└── FilesController.cs         # File uploads/downloads

Hubs/
├── ChatHub.cs                 # Real-time chat functionality
├── CallHub.cs                 # Video/voice calls (future)
└── NotificationHub.cs         # Push notifications

Services/
├── IUserService.cs            # User business logic
├── IChannelService.cs         # Channel business logic
├── IMessageService.cs         # Message business logic
├── IAuthService.cs            # Authentication logic
└── IFileService.cs            # File handling logic

Models/
├── Entities/                  # Database entities
│   ├── User.cs
│   ├── Channel.cs
│   ├── Message.cs
│   └── Friendship.cs
├── DTOs/                      # Data transfer objects
│   ├── UserDto.cs
│   ├── ChannelDto.cs
│   └── MessageDto.cs
└── Requests/                  # API request models
    ├── LoginRequest.cs
    ├── CreateChannelRequest.cs
    └── SendMessageRequest.cs
```

## 🔄 Data Flow

### Message Sending Flow
```
1. User types message in MessageInput component
2. MessageInput calls ChatService.SendMessage()
3. ChatService invokes SignalR hub method
4. Hub validates user permissions
5. Hub saves message to database
6. Hub broadcasts message to channel members
7. Clients receive message via SignalR
8. UI updates with new message
```

### Authentication Flow
```
1. User submits login form
2. AuthService sends credentials to API
3. API validates against database
4. API generates JWT token
5. Token stored in browser storage
6. Token included in subsequent requests
7. API validates token on each request
8. SignalR connection authenticated with token
```

## 💾 Database Design

### Entity Relationship Diagram
```
Users
├── Id (PK)
├── Username (Unique)
├── Email (Unique)
├── PasswordHash
├── CreatedAt
└── LastSeenAt

Channels
├── Id (PK)
├── Name
├── Description
├── IsPrivate
├── CreatedBy (FK → Users.Id)
└── CreatedAt

ChannelMembers
├── ChannelId (PK, FK → Channels.Id)
├── UserId (PK, FK → Users.Id)
├── Role (Admin, Member)
└── JoinedAt

Messages
├── Id (PK)
├── Content
├── SenderId (FK → Users.Id)
├── ChannelId (FK → Channels.Id)
├── MessageType (Text, File, System)
├── CreatedAt
└── IsDeleted

Friendships
├── Id (PK)
├── RequesterId (FK → Users.Id)
├── ReceiverId (FK → Users.Id)
├── Status (Pending, Accepted, Declined)
└── CreatedAt

Files
├── Id (PK)
├── FileName
├── FileSize
├── ContentType
├── StoragePath
├── UploadedBy (FK → Users.Id)
└── UploadedAt
```

### Database Indexes
```sql
-- Performance indexes
CREATE INDEX IX_Messages_ChannelId_CreatedAt ON Messages (ChannelId, CreatedAt DESC);
CREATE INDEX IX_Messages_SenderId ON Messages (SenderId);
CREATE INDEX IX_ChannelMembers_UserId ON ChannelMembers (UserId);
CREATE INDEX IX_Friendships_RequesterId ON Friendships (RequesterId);
CREATE INDEX IX_Friendships_ReceiverId ON Friendships (ReceiverId);
CREATE INDEX IX_Users_Username ON Users (Username);
CREATE INDEX IX_Users_Email ON Users (Email);
```

## 🔌 Integration Patterns

### Repository Pattern
```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}

public class MessageRepository : IRepository<Message>
{
    private readonly ApplicationDbContext _context;
    
    public MessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<IEnumerable<Message>> GetChannelMessagesAsync(
        Guid channelId, 
        int page, 
        int limit)
    {
        return await _context.Messages
            .Where(m => m.ChannelId == channelId && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Include(m => m.Sender)
            .ToListAsync();
    }
}
```

### Service Pattern
```csharp
public interface IChannelService
{
    Task<Channel> CreateChannelAsync(CreateChannelRequest request, Guid userId);
    Task<IEnumerable<Channel>> GetUserChannelsAsync(Guid userId);
    Task AddMemberAsync(Guid channelId, Guid userId, Guid requesterId);
    Task RemoveMemberAsync(Guid channelId, Guid userId, Guid requesterId);
}

public class ChannelService : IChannelService
{
    private readonly IRepository<Channel> _channelRepository;
    private readonly IRepository<ChannelMember> _memberRepository;
    private readonly IHubContext<ChatHub> _hubContext;
    
    public async Task<Channel> CreateChannelAsync(CreateChannelRequest request, Guid userId)
    {
        var channel = new Channel
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsPrivate = request.IsPrivate,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        
        await _channelRepository.AddAsync(channel);
        
        // Add creator as admin
        await _memberRepository.AddAsync(new ChannelMember
        {
            ChannelId = channel.Id,
            UserId = userId,
            Role = ChannelRole.Admin,
            JoinedAt = DateTime.UtcNow
        });
        
        return channel;
    }
}
```

## 🔄 SignalR Hub Architecture

### Hub Implementation
```csharp
[Authorize]
public class ChatHub : Hub
{
    private readonly IChannelService _channelService;
    private readonly IMessageService _messageService;
    private readonly IUserService _userService;
    
    public async Task JoinChannel(string channelId)
    {
        var userId = GetUserId();
        var canJoin = await _channelService.CanUserAccessChannelAsync(
            Guid.Parse(channelId), userId);
            
        if (canJoin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, channelId);
            await Clients.Group(channelId).SendAsync("UserJoined", new
            {
                UserId = userId,
                Username = GetUsername(),
                Timestamp = DateTime.UtcNow
            });
        }
    }
    
    public async Task SendMessage(string channelId, string content)
    {
        var userId = GetUserId();
        var message = await _messageService.CreateMessageAsync(
            Guid.Parse(channelId), userId, content);
            
        await Clients.Group(channelId).SendAsync("ReceiveMessage", new
        {
            Id = message.Id,
            Content = message.Content,
            SenderId = message.SenderId,
            SenderUsername = message.Sender.Username,
            Timestamp = message.CreatedAt
        });
    }
    
    private Guid GetUserId() => 
        Guid.Parse(Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        
    private string GetUsername() => 
        Context.User.FindFirst(ClaimTypes.Name)?.Value;
}
```

### Connection Management
```csharp
public class ConnectionManager
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();
    
    public void AddConnection(Guid userId, string connectionId)
    {
        _userConnections.AddOrUpdate(userId,
            new HashSet<string> { connectionId },
            (key, connections) =>
            {
                connections.Add(connectionId);
                return connections;
            });
    }
    
    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
            {
                _userConnections.TryRemove(userId, out _);
            }
        }
    }
    
    public bool IsUserOnline(Guid userId)
    {
        return _userConnections.ContainsKey(userId);
    }
}
```

## 🔐 Security Architecture

### Authentication & Authorization
```csharp
// JWT Configuration
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
        
        // SignalR token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && 
                    path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```

### Permission System
```csharp
public enum Permission
{
    ReadMessages,
    SendMessages,
    ManageChannel,
    ManageMembers,
    DeleteMessages
}

public class PermissionService
{
    public async Task<bool> HasPermissionAsync(
        Guid userId, 
        Guid channelId, 
        Permission permission)
    {
        var membership = await GetChannelMembershipAsync(userId, channelId);
        if (membership == null) return false;
        
        return permission switch
        {
            Permission.ReadMessages => true,
            Permission.SendMessages => true,
            Permission.ManageChannel => membership.Role == ChannelRole.Admin,
            Permission.ManageMembers => membership.Role == ChannelRole.Admin,
            Permission.DeleteMessages => membership.Role == ChannelRole.Admin,
            _ => false
        };
    }
}
```

## 📱 Progressive Web App Features

### Service Worker
```javascript
// sw.js
const CACHE_NAME = 'egroo-v1';
const urlsToCache = [
    '/',
    '/css/app.css',
    '/js/app.js',
    '/manifest.json'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => cache.addAll(urlsToCache))
    );
});

self.addEventListener('push', event => {
    const options = {
        body: event.data.text(),
        icon: '/icon-192.png',
        badge: '/badge-72.png',
        tag: 'chat-message',
        requireInteraction: true
    };
    
    event.waitUntil(
        self.registration.showNotification('New Message', options)
    );
});
```

### Offline Support
```csharp
public class OfflineMessageService
{
    public async Task<List<Message>> GetCachedMessagesAsync(Guid channelId)
    {
        // IndexedDB storage for offline messages
        return await JSRuntime.InvokeAsync<List<Message>>(
            "localDB.getMessages", channelId);
    }
    
    public async Task CacheMessageAsync(Message message)
    {
        await JSRuntime.InvokeVoidAsync(
            "localDB.storeMessage", message);
    }
    
    public async Task SyncPendingMessagesAsync()
    {
        var pendingMessages = await JSRuntime.InvokeAsync<List<Message>>(
            "localDB.getPendingMessages");
            
        foreach (var message in pendingMessages)
        {
            try
            {
                await ChatService.SendMessageAsync(message);
                await JSRuntime.InvokeVoidAsync(
                    "localDB.markMessageSent", message.Id);
            }
            catch
            {
                // Keep in pending state
            }
        }
    }
}
```

## 🔄 Caching Strategy

### Multi-level Caching
```csharp
public class CachingService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly IRepository<Channel> _channelRepository;
    
    public async Task<Channel?> GetChannelAsync(Guid channelId)
    {
        // L1: Memory cache
        if (_memoryCache.TryGetValue($"channel:{channelId}", out Channel? channel))
            return channel;
        
        // L2: Distributed cache (Redis)
        var cachedChannel = await _distributedCache.GetStringAsync($"channel:{channelId}");
        if (cachedChannel != null)
        {
            channel = JsonSerializer.Deserialize<Channel>(cachedChannel);
            _memoryCache.Set($"channel:{channelId}", channel, TimeSpan.FromMinutes(5));
            return channel;
        }
        
        // L3: Database
        channel = await _channelRepository.GetByIdAsync(channelId);
        if (channel != null)
        {
            await _distributedCache.SetStringAsync(
                $"channel:{channelId}",
                JsonSerializer.Serialize(channel),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
            
            _memoryCache.Set($"channel:{channelId}", channel, TimeSpan.FromMinutes(5));
        }
        
        return channel;
    }
}
```

## 📊 Monitoring & Observability

### Health Checks
```csharp
services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddCheck<SignalRHealthCheck>("signalr")
    .AddCheck<RedisHealthCheck>("redis");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

### Application Insights
```csharp
services.AddApplicationInsightsTelemetry();

// Custom telemetry
public class ChatTelemetryService
{
    private readonly TelemetryClient _telemetryClient;
    
    public void TrackMessageSent(Guid channelId, Guid userId)
    {
        _telemetryClient.TrackEvent("MessageSent", new Dictionary<string, string>
        {
            ["ChannelId"] = channelId.ToString(),
            ["UserId"] = userId.ToString()
        });
    }
}
```

## 🚀 Performance Optimizations

### Database Optimizations
- **Connection Pooling**: Reuse database connections
- **Query Optimization**: Use indexes and efficient queries
- **Pagination**: Limit result sets
- **Eager Loading**: Reduce N+1 queries

### SignalR Optimizations
- **Connection Grouping**: Organize connections by channels
- **Message Batching**: Batch multiple operations
- **Backplane Scaling**: Use Redis for multiple servers

### Frontend Optimizations
- **Component Virtualization**: Render only visible messages
- **Lazy Loading**: Load components on demand
- **Caching**: Cache frequently accessed data
- **Bundle Optimization**: Minimize JavaScript bundle size

This architecture provides a solid foundation for a scalable, maintainable chat application with real-time capabilities and modern web standards.