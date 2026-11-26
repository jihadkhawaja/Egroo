using Egroo.Server.Database;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Egroo.Server;

internal static class MaintenanceCommands
{
    internal static async Task<int> RunAsync(string command, string[] args)
    {
        bool doctor = command == "doctor";
        if (!TryParseOptions(args, doctor, out var api, out var browserOrigin))
        {
            Console.Error.WriteLine("Usage: setup or doctor [--api-url URL] [--browser-origin ORIGIN]");
            return 2;
        }

        try
        {
            var builder = WebApplication.CreateBuilder([]);
            builder.Logging.ClearProviders();
            bool local = builder.Configuration.GetValue<bool>("Egroo:LocalDebug");
            Program.ConfigureLocalDebug(builder, createSecrets: !doctor);
            if (!CheckConfiguration(builder.Configuration))
            {
                if (local && doctor)
                    Console.Error.WriteLine("Run setup with the Local Debug launch profile first.");

                return 1;
            }

            ServerDatabase.Register(builder, readOnly: doctor);
            await using var app = builder.Build();
            if (!doctor)
            {
                await Program.ApplyMigrationsAsync(app);
                Console.WriteLine("Database is up to date.");
                return 0;
            }

            bool databaseReady = await CheckDatabaseAsync(app);
            api ??= local ? new Uri("http://localhost:5176/") : null;
            if (api is null)
            {
                Console.Error.WriteLine("Pass --api-url for the running API to check HTTP and browser access.");
                return 2;
            }

            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                BaseAddress = api,
                Timeout = TimeSpan.FromSeconds(8),
                MaxResponseContentBufferSize = 64 * 1024
            };

            bool apiReady = await CheckApiAsync(http);
            bool browserReady = browserOrigin is null || await CheckBrowserOriginAsync(http, browserOrigin);
            if (browserOrigin is null)
                Console.WriteLine("Browser origin was skipped. Pass --browser-origin to check it.");

            return databaseReady && apiReady && browserReady ? 0 : 1;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException
            or FormatException or UnauthorizedAccessException or System.Data.Common.DbException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine(doctor
                ? "Could not load the server configuration. Check the launch profile and database settings."
                : "Setup failed. Check server configuration, database access, and migration permissions.");
            return 1;
        }
    }

    private static bool CheckConfiguration(IConfiguration configuration)
    {
        if (Encoding.UTF8.GetByteCount(configuration["Secrets:Jwt"] ?? "") >= 32
            && HasAsciiLength(configuration["Encryption:Key"], 32)
            && HasAsciiLength(configuration["Encryption:IV"], 16))
        {
            Console.WriteLine("Server configuration is valid.");
            return true;
        }

        Console.Error.WriteLine("Configure Secrets:Jwt (at least 32 UTF-8 bytes), Encryption:Key (32 ASCII characters), and Encryption:IV (16 ASCII characters).");
        return false;
    }

    private static bool HasAsciiLength(string? value, int length)
        => value?.Length == length && value.All(character => character <= 127);

    private static async Task<bool> CheckDatabaseAsync(WebApplication app)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        try
        {
            await db.Database.OpenConnectionAsync(timeout.Token);
            if ((await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any())
            {
                Console.Error.WriteLine("Database is reachable, but migrations are pending. Run setup.");
                return false;
            }

            Console.WriteLine("Database is reachable and up to date.");
            return true;
        }
        catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException or OperationCanceledException)
        {
            Console.Error.WriteLine("Could not read the database. Check the configured database path or PostgreSQL connectivity, and run setup for a new database.");
            return false;
        }
    }

    private static async Task<bool> CheckApiAsync(HttpClient http)
    {
        try
        {
            using var response = await http.GetAsync("runtime/index.html");
            if (response.IsSuccessStatusCode
                && (await response.Content.ReadAsStringAsync()).Contains("src=\"clientRuntime.js\"", StringComparison.Ordinal))
            {
                Console.WriteLine("Egroo API runtime is reachable.");
                return true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // Report the check without including URL or provider exception details.
        }

        Console.Error.WriteLine("Could not reach the Egroo API runtime. Check the API address, TLS, and proxy routing for /runtime/.");
        return false;
    }

    private static async Task<bool> CheckBrowserOriginAsync(HttpClient http, string origin)
    {
        if (http.BaseAddress!.GetLeftPart(UriPartial.Authority).Equals(origin, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Browser and API use the same origin.");
            return true;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Options, "api/v1/Auth/refreshsession");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "GET");
            request.Headers.Add("Access-Control-Request-Headers", "authorization");

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode
                && (Allows(response, "Access-Control-Allow-Origin", origin)
                    || Allows(response, "Access-Control-Allow-Origin", "*"))
                && Allows(response, "Access-Control-Allow-Methods", "GET")
                && Allows(response, "Access-Control-Allow-Headers", "authorization"))
            {
                Console.WriteLine("Browser origin is accepted by the API.");
                return true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // The setup guidance below applies to blocked and unavailable preflights.
        }

        Console.Error.WriteLine("Browser preflight failed. Check Api:AllowedOrigins and ensure the proxy forwards OPTIONS to the API.");
        return false;
    }

    private static bool Allows(HttpResponseMessage response, string header, string value)
        => response.Headers.TryGetValues(header, out var values)
            && values.SelectMany(item => item.Split(',', StringSplitOptions.TrimEntries))
                .Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool TryParseOptions(string[] args, bool doctor, out Uri? api, out string? origin)
    {
        api = null;
        origin = null;
        if (!doctor)
            return args.Length == 0;

        for (int index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !Uri.TryCreate(args[index + 1], UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https") || uri.Host.Length == 0
                || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                return false;

            switch (args[index])
            {
                case "--api-url" when api is null:
                    api = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
                    break;
                case "--browser-origin" when origin is null && uri.AbsolutePath == "/":
                    origin = uri.GetLeftPart(UriPartial.Authority);
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
