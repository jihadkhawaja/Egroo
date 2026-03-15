using Egroo.Server.Services;

namespace Egroo.Server.Test
{
    [TestClass]
    public class ChannelFileStorageTest
    {
        private ChannelFileStorageService _service = null!;
        private string _storageRoot = null!;

        [TestInitialize]
        public void Initialize()
        {
            var services = TestServiceProvider.Build(dbName: $"FileStorageDb_{Guid.NewGuid():N}");
            _service = services.GetRequiredService<ChannelFileStorageService>();
            _storageRoot = Path.Combine(AppContext.BaseDirectory, "App_Data", "channel-files");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_storageRoot))
            {
                try { Directory.Delete(_storageRoot, recursive: true); } catch { }
            }
        }

        // ── TryResolveFile ─────────────────────────────────────────────────────────

        [TestMethod]
        public void TryResolveFile_NonexistentFile_ReturnsFalse()
        {
            bool result = _service.TryResolveFile(
                Guid.NewGuid(), "abc123", "file.txt",
                out var filePath, out var contentType, out var downloadName);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryResolveFile_TraversalToken_ReturnsFalse()
        {
            bool result = _service.TryResolveFile(
                Guid.NewGuid(), "../../../etc", "passwd",
                out var filePath, out var contentType, out var downloadName);

            Assert.IsFalse(result);
            Assert.AreEqual(string.Empty, filePath);
        }

        [TestMethod]
        public void TryResolveFile_EmptyToken_ReturnsFalse()
        {
            bool result = _service.TryResolveFile(
                Guid.NewGuid(), "", "file.txt",
                out var filePath, out var contentType, out var downloadName);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryResolveFile_TokenWithBackslash_ReturnsFalse()
        {
            bool result = _service.TryResolveFile(
                Guid.NewGuid(), @"abc\def", "file.txt",
                out var filePath, out var contentType, out var downloadName);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryResolveFile_TokenWithForwardSlash_ReturnsFalse()
        {
            bool result = _service.TryResolveFile(
                Guid.NewGuid(), "abc/def", "file.txt",
                out var filePath, out var contentType, out var downloadName);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void TryResolveFile_ExistingFile_ReturnsTrue()
        {
            var channelId = Guid.NewGuid();
            string token = Guid.NewGuid().ToString("N");
            string fileName = "testfile.txt";
            string folderPath = Path.Combine(_storageRoot, channelId.ToString("N"), token);
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, fileName), "test content");

            bool result = _service.TryResolveFile(
                channelId, token, fileName,
                out var filePath, out var contentType, out var downloadName);

            Assert.IsTrue(result);
            Assert.AreEqual(fileName, downloadName);
            Assert.AreEqual("text/plain", contentType);
        }

        [TestMethod]
        public void TryResolveFile_KnownExtension_ReturnsCorrectContentType()
        {
            var channelId = Guid.NewGuid();
            string token = Guid.NewGuid().ToString("N");
            string fileName = "document.pdf";
            string folderPath = Path.Combine(_storageRoot, channelId.ToString("N"), token);
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, fileName), "dummy");

            _service.TryResolveFile(
                channelId, token, fileName,
                out _, out var contentType, out _);

            Assert.AreEqual("application/pdf", contentType);
        }

        [TestMethod]
        public void TryResolveFile_UnknownExtension_ReturnsOctetStream()
        {
            var channelId = Guid.NewGuid();
            string token = Guid.NewGuid().ToString("N");
            string fileName = "data.xyz";
            string folderPath = Path.Combine(_storageRoot, channelId.ToString("N"), token);
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, fileName), "dummy");

            _service.TryResolveFile(
                channelId, token, fileName,
                out _, out var contentType, out _);

            Assert.AreEqual("application/octet-stream", contentType);
        }

        // ── SaveAsync ───────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task SaveAsync_ValidFile_ReturnsFileLink()
        {
            var channelId = Guid.NewGuid();
            var formFile = CreateFormFile("hello.txt", "Hello, world!", "text/plain");

            var link = await _service.SaveAsync(channelId, formFile);

            Assert.IsNotNull(link);
            Assert.AreEqual(channelId, link.ChannelId);
            Assert.AreEqual("hello.txt", link.FileName);
            Assert.AreEqual("text/plain", link.ContentType);
            Assert.IsTrue(link.SizeBytes > 0);
            Assert.IsTrue(link.Url!.StartsWith("/api/v1/ChannelFiles/"));
        }

        [TestMethod]
        public async Task SaveAsync_EmptyFile_ReturnsNull()
        {
            var channelId = Guid.NewGuid();
            var formFile = CreateFormFile("empty.txt", "", "text/plain");

            var link = await _service.SaveAsync(channelId, formFile);

            Assert.IsNull(link);
        }

        [TestMethod]
        public async Task SaveAsync_OversizedFile_ReturnsNull()
        {
            var channelId = Guid.NewGuid();
            // Create a file larger than 1 MB
            var content = new string('x', (int)ChannelFileStorageService.MaxFileSizeBytes + 1);
            var formFile = CreateFormFile("large.bin", content, "application/octet-stream");

            var link = await _service.SaveAsync(channelId, formFile);

            Assert.IsNull(link);
        }

        [TestMethod]
        public async Task SaveAsync_NoContentType_FallsBackToExtension()
        {
            var channelId = Guid.NewGuid();
            var formFile = CreateFormFile("image.png", "fake-png-data", contentType: "");

            var link = await _service.SaveAsync(channelId, formFile);

            Assert.IsNotNull(link);
            Assert.AreEqual("image/png", link.ContentType);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private static Microsoft.AspNetCore.Http.IFormFile CreateFormFile(string fileName, string content, string contentType)
        {
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            return new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                ContentType = contentType
            };
        }
    }
}
