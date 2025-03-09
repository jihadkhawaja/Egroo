using jihadkhawaja.chat.server.Database;
using jihadkhawaja.chat.server.Hubs;
using jihadkhawaja.chat.server.Interfaces;
using jihadkhawaja.chat.server.Services;
using jihadkhawaja.chat.server.SIP;
using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

public enum DatabaseEnum
{
    Postgres,
    SqlServer
}

public static class Register
{
    public static ChatServiceBuilder ChatService { get; private set; } = null!;

    public static ChatServiceBuilder AddChatServices(this IServiceCollection services)
    {
        ChatService = new ChatServiceBuilder(services);
        return ChatService;
    }

    public static void UseChatServices(this WebApplication app)
    {
        // Auto-migrate database.
        if (ChatService.AutoMigrateDatabase)
        {
            using (IServiceScope scope = app.Services.CreateScope())
            {
                DataContext db = scope.ServiceProvider.GetRequiredService<DataContext>();
                db.Database.Migrate();
            }
        }

        // Map SignalR hubs.
        app.MapHub<ChatHub>("/chathub", options =>
        {
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
        });

        // Start the SIP (SDP exchange) WebSocket service if it was configured.
        if (ChatService.WSServer != null)
        {
            ChatService.WSServer.Start();
            Console.WriteLine("WebSocket SDP exchange server started on ws://{0}:{1}/",
                ChatService.WSServer.Address, ChatService.WSServer.Port);
        }
    }

    // --- SDPExchange Event Handlers ---
    internal static async Task<SIPSorcery.Net.RTCPeerConnection> SendSDPOffer(WebSocketContext context)
    {
        Console.WriteLine("SendSDPOffer: WebSocket connection opened from " + context.UserEndPoint);
        // For signaling only, you might create a minimal RTCPeerConnection.
        // (In a full media relay scenario, you would configure the connection further.)
        var pc = new SIPSorcery.Net.RTCPeerConnection(null);
        return await Task.FromResult(pc);
    }

    internal static void WebSocketMessageReceived(SIPSorcery.Net.RTCPeerConnection pc, string message)
    {
        Console.WriteLine("WebSocketMessageReceived: Received message: " + message);
        // Process the message as needed (e.g. if it's an SDP answer or ICE candidate).
    }
}

public class ChatServiceBuilder
{
    public string? DbConnectionStringKey { get; private set; }
    public DatabaseEnum SelectedDatabase { get; private set; }
    public string? CurrentExecutionAssemblyName { get; private set; }
    public bool AutoMigrateDatabase { get; private set; }
    public WebSocketServer? WSServer { get; private set; }

    private readonly IServiceCollection _services;
    private IConfiguration? _configuration;
    private Type? _executionClassType;
    private DatabaseEnum _databaseEnum;
    private bool _autoMigrateDatabase = true;
    private string _dbConnectionStringKey = "DefaultConnection";

    public ChatServiceBuilder(IServiceCollection services)
    {
        _services = services;
    }

    public ChatServiceBuilder WithConfiguration(IConfiguration config)
    {
        _configuration = config;
        return this;
    }

    public ChatServiceBuilder WithExecutionClassType(Type executionClassType)
    {
        _executionClassType = executionClassType;
        return this;
    }

    public ChatServiceBuilder WithDatabase(DatabaseEnum databaseEnum)
    {
        _databaseEnum = databaseEnum;
        return this;
    }

    public ChatServiceBuilder WithAutoMigrateDatabase(bool autoMigrateDatabase)
    {
        _autoMigrateDatabase = autoMigrateDatabase;
        return this;
    }

    public ChatServiceBuilder WithDbConnectionStringKey(string dbConnectionStringKey)
    {
        _dbConnectionStringKey = dbConnectionStringKey;
        return this;
    }

    public IServiceCollection Build()
    {
        DbConnectionStringKey = _dbConnectionStringKey;
        SelectedDatabase = _databaseEnum;
        CurrentExecutionAssemblyName =
            System.Reflection.Assembly.GetAssembly(_executionClassType).GetName().Name;
        AutoMigrateDatabase = _autoMigrateDatabase;

        ConfigureEntityServices(_services);
        ConfigureDatabase(_services);
        ConfigureSignalR(_services);
        // ConfigureSIPWebSocket(_services);

        return _services;
    }

    private void ConfigureSignalR(IServiceCollection services)
    {
        services.AddSignalR();
    }

    private void ConfigureDatabase(IServiceCollection services)
    {
        services.AddDbContext<DataContext>();
    }

    private void ConfigureEntityServices(IServiceCollection services)
    {
        services.AddScoped<IEntity<User>, EntityService<User>>();
        services.AddScoped<IEntity<UserFriend>, EntityService<UserFriend>>();
        services.AddScoped<IEntity<Channel>, EntityService<Channel>>();
        services.AddScoped<IEntity<ChannelUser>, EntityService<ChannelUser>>();
        services.AddScoped<IEntity<Message>, EntityService<Message>>();
        services.AddScoped<IEntity<UserPendingMessage>, EntityService<UserPendingMessage>>();
    }

    private void ConfigureSIPWebSocket(IServiceCollection services)
    {
        // Create and configure the WebSocket server for SDP exchange.
        WSServer = new WebSocketServer(IPAddress.Any, 8081, false);
        WSServer.AddWebSocketService<SDPExchange>("/", sdpExchanger =>
        {
            // Register event handlers for SDP exchange.
            sdpExchanger.WebSocketOpened += Register.SendSDPOffer;
            sdpExchanger.OnMessageReceived += Register.WebSocketMessageReceived;
        });
    }
}
