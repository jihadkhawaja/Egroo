using System.Text;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Creates and manages skill files stored under the server-managed skills workspace.
    /// </summary>
    public sealed class AgentManagedSkillsService
    {
        private const string ManagedSkillsRoot = "skills/managed/agents";
        private const int MaxSkillFileBytes = 256 * 1024;

        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AgentManagedSkillsService> _logger;

        public AgentManagedSkillsService(
            IWebHostEnvironment environment,
            ILogger<AgentManagedSkillsService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public ManagedSkillFile CreateManagedSkill(Guid agentId, string skillName, string content, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Skill content cannot be empty.", nameof(content));
            }

            var normalizedContent = content.Trim();
            if (Encoding.UTF8.GetByteCount(normalizedContent) > MaxSkillFileBytes)
            {
                throw new ArgumentException($"Skill content must be {MaxSkillFileBytes / 1024} KB or smaller.", nameof(content));
            }

            var normalizedSkillName = ResolveSkillName(skillName, normalizedContent, fileName);
            if (string.IsNullOrWhiteSpace(normalizedSkillName))
            {
                throw new ArgumentException("A skill name is required.", nameof(skillName));
            }

            var skillSlug = Slugify(normalizedSkillName);
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var directoryName = $"{skillSlug}-{uniqueSuffix}";
            var relativeDirectory = NormalizeRelativePath(Path.Combine(ManagedSkillsRoot, agentId.ToString("N"), directoryName));
            var fullDirectory = GetRunLocationPath(relativeDirectory);

            Directory.CreateDirectory(fullDirectory);

            var markdown = EnsureSkillMarkdown(normalizedSkillName, normalizedContent, fileName);
            var fullFilePath = Path.Combine(fullDirectory, "SKILL.md");
            File.WriteAllText(fullFilePath, markdown, new UTF8Encoding(false));

            _logger.LogInformation("Created managed skill for agent {AgentId} at {Path}", agentId, fullDirectory);
            return new ManagedSkillFile(relativeDirectory, "SKILL.md", normalizedSkillName);
        }

        public bool IsManagedSkillPath(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            return NormalizeRelativePath(relativePath)
                .StartsWith(ManagedSkillsRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        public void DeleteManagedSkillDirectory(string relativePath)
        {
            if (!IsManagedSkillPath(relativePath))
            {
                return;
            }

            var normalizedPath = NormalizeRelativePath(relativePath);
            var fullDirectory = GetRunLocationPath(normalizedPath);
            var managedRoot = GetRunLocationPath(ManagedSkillsRoot);

            if (fullDirectory.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullDirectory))
            {
                Directory.Delete(fullDirectory, recursive: true);
                _logger.LogInformation("Deleted managed skill directory {Path}", fullDirectory);
            }

            var legacyDirectory = GetLegacyContentRootPath(normalizedPath);
            var legacyManagedRoot = GetLegacyContentRootPath(ManagedSkillsRoot);
            if (legacyDirectory.StartsWith(legacyManagedRoot, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(legacyDirectory)
                && !legacyDirectory.Equals(fullDirectory, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(legacyDirectory, recursive: true);
                _logger.LogInformation("Deleted legacy managed skill directory {Path}", legacyDirectory);
            }
        }

        private static string EnsureSkillMarkdown(string skillName, string content, string? fileName)
        {
            if (HasFrontMatter(content))
            {
                return content;
            }

            var heading = string.IsNullOrWhiteSpace(fileName)
                ? skillName.Trim()
                : Path.GetFileNameWithoutExtension(fileName).Replace('-', ' ').Trim();

            var escapedHeading = string.IsNullOrWhiteSpace(heading) ? skillName.Trim() : heading;
            var slug = Slugify(skillName);

            return $"---\nname: {slug}\ndescription: 'User-managed skill created in Egroo.'\nargument-hint: 'Describe what you need help with.'\n---\n\n# {escapedHeading}\n\n{content}\n";
        }

        private static string ResolveSkillName(string? skillName, string content, string? fileName)
        {
            var trimmedSkillName = skillName?.Trim();
            var extractedContentName = ExtractSkillNameFromContent(content);

            if (!string.IsNullOrWhiteSpace(extractedContentName)
                && (string.IsNullOrWhiteSpace(trimmedSkillName) || IsPlaceholderSkillName(trimmedSkillName, fileName)))
            {
                return extractedContentName;
            }

            if (!string.IsNullOrWhiteSpace(trimmedSkillName))
            {
                return trimmedSkillName;
            }

            var fileNameCandidate = ExtractSkillNameFromFileName(fileName);
            if (!string.IsNullOrWhiteSpace(fileNameCandidate))
            {
                return fileNameCandidate;
            }

            return extractedContentName;
        }

        private static string? ExtractSkillNameFromContent(string content)
        {
            if (TryExtractFrontMatterName(content, out var frontMatterName))
            {
                return frontMatterName;
            }

            foreach (var line in content.Split('\n'))
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("# ", StringComparison.Ordinal))
                {
                    return trimmedLine[2..].Trim();
                }
            }

            return null;
        }

        private static bool TryExtractFrontMatterName(string content, out string? skillName)
        {
            skillName = null;

            if (!HasFrontMatter(content))
            {
                return false;
            }

            var lines = content.Split('\n');
            for (var index = 1; index < lines.Length; index++)
            {
                var trimmedLine = lines[index].Trim();
                if (trimmedLine == "---")
                {
                    break;
                }

                if (!trimmedLine.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = trimmedLine[5..].Trim().Trim('\'', '"');
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                skillName = value;
                return true;
            }

            return false;
        }

        private static string? ExtractSkillNameFromFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var baseName = Path.GetFileNameWithoutExtension(fileName).Trim();
            if (IsPlaceholderSkillName(baseName, fileName))
            {
                return null;
            }

            return baseName.Replace('-', ' ').Replace('_', ' ').Trim();
        }

        private static bool IsPlaceholderSkillName(string skillName, string? fileName)
        {
            var normalizedSkillName = Slugify(skillName);
            if (normalizedSkillName == "skill")
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var normalizedFileName = Slugify(Path.GetFileNameWithoutExtension(fileName));
            return normalizedFileName == "skill" && normalizedSkillName == normalizedFileName;
        }

        private static bool HasFrontMatter(string content)
        {
            var trimmed = content.TrimStart();
            return trimmed.StartsWith("---\r\n", StringComparison.Ordinal)
                || trimmed.StartsWith("---\n", StringComparison.Ordinal);
        }

        private static string Slugify(string value)
        {
            var builder = new StringBuilder();
            var lastWasDash = false;

            foreach (var character in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    lastWasDash = false;
                    continue;
                }

                if (lastWasDash)
                {
                    continue;
                }

                builder.Append('-');
                lastWasDash = true;
            }

            var slug = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "skill" : slug;
        }

        private static string GetRunLocationPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private string GetLegacyContentRootPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string NormalizeRelativePath(string path) => path.Replace('\\', '/').Trim();
    }

    public sealed record ManagedSkillFile(string RelativeDirectoryPath, string FileName, string DisplayName);
}