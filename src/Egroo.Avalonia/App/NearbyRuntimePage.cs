using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Egroo.Avalonia;

internal sealed class NearbyRuntimePage : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly byte[] _response;

    public NearbyRuntimePage()
    {
        _response = BuildResponse();
        _listener.Start(8);
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        var path = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        Address = new Uri($"http://127.0.0.1:{port}/{path}/index.html");
        _ = ServeAsync(_shutdown.Token);
    }

    public Uri Address { get; }

    private static byte[] BuildResponse()
    {
        string ReadScript(string name)
        {
            using var reader = new StreamReader(typeof(NearbyRuntimePage).Assembly
                .GetManifestResourceStream($"Egroo.Avalonia.Nearby.{name}.js")!);
            return reader.ReadToEnd().ReplaceLineEndings("\n");
        }

        static string ScriptHash(string script) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

        var nearby = ReadScript("nearbyRtc");
        var runtime = ReadScript("clientRuntime");
        var html = $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><title>Egroo Nearby</title></head>"
            + $"<body><script>{nearby}</script><script>{runtime}</script></body></html>";
        var headers = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
            + $"Content-Length: {Encoding.UTF8.GetByteCount(html)}\r\nConnection: close\r\nCache-Control: no-store\r\n"
            + "X-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nCross-Origin-Resource-Policy: same-origin\r\n"
            + "Permissions-Policy: microphone=(self), camera=()\r\n"
            + "Content-Security-Policy: default-src 'none'; media-src blob:; frame-ancestors 'none'; "
            + $"script-src 'sha256-{ScriptHash(nearby)}' 'sha256-{ScriptHash(runtime)}'\r\n\r\n";
        return Encoding.UTF8.GetBytes(headers + html);
    }

    private async Task ServeAsync(CancellationToken stopping)
    {
        var buffer = new byte[8192];
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(stopping).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                using var stream = client.GetStream();
                if (await IsPageRequestAsync(stream, buffer, timeout.Token).ConfigureAwait(false))
                    await stream.WriteAsync(_response, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException
                || (ex is ObjectDisposedException && stopping.IsCancellationRequested))
            {
                // An abandoned local request must not prevent a later navigation from loading.
            }
        }
    }

    private async Task<bool> IsPageRequestAsync(NetworkStream stream, byte[] buffer, CancellationToken token)
    {
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), token).ConfigureAwait(false);
            if (read == 0)
                return false;

            length += read;
            var headerEnd = buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8);
            if (headerEnd < 0)
                continue;

            var lines = Encoding.ASCII.GetString(buffer, 0, headerEnd).Split("\r\n");
            return lines[0] == $"GET {Address.AbsolutePath} HTTP/1.1"
                && lines.Skip(1).Any(line => line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
                    && line.AsSpan(5).Trim().Equals(Address.Authority, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _listener.Stop();
        _shutdown.Dispose();
    }
}
