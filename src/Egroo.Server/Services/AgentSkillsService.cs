using Microsoft.Agents.AI;
using jihadkhawaja.chat.shared.Models;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Resolves and validates Agent Skills directories before attaching them as AI context providers.
    /// </summary>
    public class AgentSkillsService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<AgentSkillsService> _logger;

        public AgentSkillsService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILoggerFactory loggerFactory,
            ILogger<AgentSkillsService> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _loggerFactory = loggerFactory;
            _logger = logger;
        }

        public IReadOnlyList<string> ResolveSkillPaths(IEnumerable<AgentSkillDirectory> skillDirectories)
        {
            var allowedRoots = GetAllowedRootPaths();
            var resolved = new List<string>();

            foreach (var directory in skillDirectories.Where(x => x.IsEnabled && !string.IsNullOrWhiteSpace(x.Path)))
            {
                string fullPath;
                try
                {
                    fullPath = ResolveFullPath(directory.Path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid skill directory path for agent {AgentId}: {Path}", directory.AgentDefinitionId, directory.Path);
                    continue;
                }

                if (!Directory.Exists(fullPath))
                {
                    _logger.LogWarning("Skipping missing skill directory for agent {AgentId}: {Path}", directory.AgentDefinitionId, fullPath);
                    continue;
                }

                if (allowedRoots.Count > 0 && !allowedRoots.Any(root => IsPathWithinRoot(fullPath, root)))
                {
                    _logger.LogWarning("Skipping skill directory outside allowed roots for agent {AgentId}: {Path}", directory.AgentDefinitionId, fullPath);
                    continue;
                }

                if (!resolved.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                {
                    resolved.Add(fullPath);
                }
            }

            return resolved;
        }

        public IReadOnlyList<AIContextProvider> CreateContextProviders(IEnumerable<AgentSkillDirectory> skillDirectories, string? skillsInstructionPrompt)
        {
            var skillPaths = ResolveSkillPaths(skillDirectories);
            if (skillPaths.Count == 0)
            {
                return Array.Empty<AIContextProvider>();
            }

#pragma warning disable MAAI001
            var options = new FileAgentSkillsProviderOptions();
            if (!string.IsNullOrWhiteSpace(skillsInstructionPrompt))
            {
                if (skillsInstructionPrompt.Contains("{0}", StringComparison.Ordinal))
                {
                    options.SkillsInstructionPrompt = skillsInstructionPrompt;
                }
                else
                {
                    _logger.LogWarning("Ignoring custom skills prompt because it does not contain the required {{0}} placeholder.");
                }
            }

            return new AIContextProvider[]
            {
                new FileAgentSkillsProvider(skillPaths, options, _loggerFactory)
            };
#pragma warning restore MAAI001
        }

        private IReadOnlyList<string> GetAllowedRootPaths()
        {
            var configuredRoots = _configuration.GetSection("AgentSkills:AllowedRootPaths").Get<string[]>() ?? Array.Empty<string>();

            return configuredRoots
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(ResolveAllowedRootPaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string ResolveFullPath(string path)
        {
            var trimmedPath = path.Trim();
            if (Path.IsPathRooted(trimmedPath))
            {
                return Path.GetFullPath(trimmedPath);
            }

            if (UsesRunLocationBase(trimmedPath))
            {
                var runLocationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, trimmedPath));
                var contentRootPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, trimmedPath));

                if (Directory.Exists(runLocationPath))
                {
                    return runLocationPath;
                }

                if (Directory.Exists(contentRootPath))
                {
                    return contentRootPath;
                }

                return runLocationPath;
            }

            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, trimmedPath));
        }

        private IEnumerable<string> ResolveAllowedRootPaths(string path)
        {
            var trimmedPath = path.Trim();
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                yield break;
            }

            if (Path.IsPathRooted(trimmedPath))
            {
                yield return Path.GetFullPath(trimmedPath);
                yield break;
            }

            if (UsesRunLocationBase(trimmedPath))
            {
                yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, trimmedPath));

                var contentRootPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, trimmedPath));
                if (!contentRootPath.Equals(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, trimmedPath)), StringComparison.OrdinalIgnoreCase))
                {
                    yield return contentRootPath;
                }

                yield break;
            }

            yield return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, trimmedPath));
        }

        private static bool UsesRunLocationBase(string path)
        {
                var normalized = path.Replace('\\', '/').TrimStart('.', '/');
            return normalized.Equals("skills", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("skills/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathWithinRoot(string path, string root)
        {
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}