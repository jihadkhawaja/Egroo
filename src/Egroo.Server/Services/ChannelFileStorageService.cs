using jihadkhawaja.chat.shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

namespace Egroo.Server.Services
{
    public class ChannelFileStorageService
    {
        public const long MaxFileSizeBytes = 25 * 1024 * 1024;

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ChannelFileStorageService> _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

        public ChannelFileStorageService(
            IWebHostEnvironment environment,
            ILogger<ChannelFileStorageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        private string StorageRoot => Path.Combine(AppContext.BaseDirectory, "App_Data", "channel-files");

        public async Task<ChannelFileLink?> SaveAsync(Guid channelId, IFormFile file, CancellationToken cancellationToken = default)
        {
            if (file.Length <= 0 || file.Length > MaxFileSizeBytes)
            {
                return null;
            }

            string safeFileName = SanitizeFileName(file.FileName);
            string token = Guid.NewGuid().ToString("N");
            string folderPath = Path.Combine(StorageRoot, channelId.ToString("N"), token);
            Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, safeFileName);

            await using (var targetStream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(targetStream, cancellationToken);
            }

            string contentType = !string.IsNullOrWhiteSpace(file.ContentType)
                ? file.ContentType
                : GetContentType(safeFileName);

            _logger.LogInformation("Stored channel file {FileName} for channel {ChannelId}", safeFileName, channelId);

            return new ChannelFileLink
            {
                ChannelId = channelId,
                FileName = safeFileName,
                ContentType = contentType,
                SizeBytes = file.Length,
                Url = $"/api/v1/ChannelFiles/{channelId:D}/{token}/{Uri.EscapeDataString(safeFileName)}"
            };
        }

        public bool TryResolveFile(Guid channelId, string token, string fileName, out string filePath, out string contentType, out string downloadFileName)
        {
            string safeFileName = SanitizeFileName(Uri.UnescapeDataString(fileName));
            downloadFileName = safeFileName;
            filePath = Path.Combine(StorageRoot, channelId.ToString("N"), token, safeFileName);
            contentType = GetContentType(safeFileName);

            return File.Exists(filePath);
        }

        private string GetContentType(string fileName)
        {
            return _contentTypeProvider.TryGetContentType(fileName, out var contentType)
                ? contentType
                : "application/octet-stream";
        }

        private static string SanitizeFileName(string? fileName)
        {
            string candidate = string.IsNullOrWhiteSpace(fileName)
                ? "file"
                : Path.GetFileName(fileName.Trim());

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                candidate = candidate.Replace(invalidChar, '_');
            }

            return string.IsNullOrWhiteSpace(candidate) ? "file" : candidate;
        }
    }
}