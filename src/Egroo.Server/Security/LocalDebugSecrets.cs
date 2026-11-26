using System.Security.Cryptography;
using System.Text.Json;

namespace Egroo.Server.Security;

internal static class LocalDebugSecrets
{
    internal static void Apply(IConfiguration configuration, string directory, bool createIfMissing = true)
    {
        string path = Path.Combine(directory, "local-debug-secrets.json");
        if (!File.Exists(path))
        {
            if (!createIfMissing)
                return;

            Directory.CreateDirectory(directory);
            CreateFile(path);
        }

        using var file = File.OpenRead(path);
        var secrets = JsonSerializer.Deserialize<Secrets>(file)
            ?? throw new InvalidOperationException("The local debug secrets file is empty.");
        configuration["Secrets:Jwt"] = secrets.Jwt;
        configuration["Encryption:Key"] = secrets.Key;
        configuration["Encryption:IV"] = secrets.IV;
    }

    private static void CreateFile(string path)
    {
        string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        var secrets = new Secrets(RandomNumberGenerator.GetHexString(64), RandomNumberGenerator.GetHexString(32), RandomNumberGenerator.GetHexString(16));
        try
        {
            using (var file = new FileStream(temporaryPath, options))
            {
                JsonSerializer.Serialize(file, secrets);
                file.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another startup created the file first.
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private sealed record Secrets(string Jwt, string Key, string IV);
}
