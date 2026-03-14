using Egroo.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Egroo.Server.Test
{
    [TestClass]
    public class AgentSkillsTest
    {
        private const string DbName = "AgentSkillsTestDb";

        [TestMethod]
        public async Task AgentRepository_CanRoundTripSkillDirectories()
        {
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: DbName, authenticatedUserId: userId);

            using var scope = services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

            var agent = await repo.CreateAgent(new AgentDefinition
            {
                Name = "skills-agent",
                Model = "gpt-4o-mini",
                Provider = LlmProvider.OpenAI,
                ApiKey = "test-key",
                SkillsInstructionPrompt = "Skills available:\n{0}"
            });

            Assert.IsNotNull(agent, "Agent should be created.");
            Assert.AreEqual("Skills available:\n{0}", agent.SkillsInstructionPrompt);

            var added = await repo.AddSkillDirectory(new AgentSkillDirectory
            {
                AgentDefinitionId = agent.Id,
                Name = "Team Skills",
                Path = "skills/team",
                IsEnabled = true
            });

            Assert.IsNotNull(added, "Skill directory should be added.");

            var directories = await repo.GetAgentSkillDirectories(agent.Id);
            Assert.IsNotNull(directories);
            Assert.AreEqual(1, directories.Length);
            Assert.AreEqual("Team Skills", directories[0].Name);
            Assert.AreEqual("skills/team", directories[0].Path);

            directories[0].Name = "Shared Skills";
            directories[0].IsEnabled = false;
            directories[0].Path = "skills/shared";

            var updated = await repo.UpdateSkillDirectory(directories[0]);
            Assert.IsTrue(updated, "Skill directory update should succeed.");

            var updatedDirectories = await repo.GetAgentSkillDirectories(agent.Id);
            Assert.IsNotNull(updatedDirectories);
            Assert.AreEqual("Shared Skills", updatedDirectories[0].Name);
            Assert.AreEqual("skills/shared", updatedDirectories[0].Path);
            Assert.IsFalse(updatedDirectories[0].IsEnabled);
        }

        [TestMethod]
        public void AgentSkillsService_ResolvesOnlyAllowedDirectories()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-agent-skills", Guid.NewGuid().ToString("N"));
            var allowedRoot = Path.Combine(tempRoot, "allowed");
            var validSkillDir = Path.Combine(allowedRoot, "finance-skill");
            var blockedSkillDir = Path.Combine(tempRoot, "blocked", "secret-skill");

            Directory.CreateDirectory(validSkillDir);
            Directory.CreateDirectory(blockedSkillDir);

            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AgentSkills:AllowedRootPaths:0"] = allowedRoot
                    })
                    .Build();

                var environment = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, environment, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var resolved = service.ResolveSkillPaths(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Finance Skill", Path = Path.Combine("allowed", "finance-skill"), IsEnabled = true },
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Secret Skill", Path = Path.Combine("blocked", "secret-skill"), IsEnabled = true }
                });

                Assert.AreEqual(1, resolved.Count);
                Assert.AreEqual(Path.GetFullPath(validSkillDir), resolved[0]);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        [TestMethod]
        public void AgentManagedSkillsService_CreatesWrappedSkillFile()
        {
            var agentId = Guid.NewGuid();
            string? savedDirectory = null;

            try
            {
                var environment = new TestEnvironment { ContentRootPath = Path.GetTempPath() };
                var service = new AgentManagedSkillsService(environment, NullLogger<AgentManagedSkillsService>.Instance);

                var result = service.CreateManagedSkill(agentId, "Release Notes Helper", "## When to use\n- Summarize release notes");
                savedDirectory = Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar));
                var savedFile = Path.Combine(savedDirectory, result.FileName);
                var savedContent = File.ReadAllText(savedFile);

                StringAssert.Contains(savedContent, "name: release-notes-helper");
                StringAssert.Contains(savedContent, "# Release Notes Helper");
                StringAssert.Contains(savedContent, "## When to use");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(savedDirectory) && Directory.Exists(savedDirectory))
                {
                    Directory.Delete(savedDirectory, recursive: true);
                }
            }
        }

        [TestMethod]
        public void AgentManagedSkillsService_PreservesFrontMatterAndDeletesManagedDirectory()
        {
            var agentId = Guid.NewGuid();
            string? savedDirectory = null;

            try
            {
                var environment = new TestEnvironment { ContentRootPath = Path.GetTempPath() };
                var service = new AgentManagedSkillsService(environment, NullLogger<AgentManagedSkillsService>.Instance);
                const string existingSkill = "---\nname: custom-skill\ndescription: 'Custom skill.'\nargument-hint: 'Ask for help.'\n---\n\n# Custom Skill\n\nUse the existing content.";

                var result = service.CreateManagedSkill(agentId, "Custom Skill", existingSkill, "SKILL.md");
                savedDirectory = Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar));
                var savedFile = Path.Combine(savedDirectory, result.FileName);
                var savedContent = File.ReadAllText(savedFile);

                Assert.AreEqual(existingSkill, savedContent);
                Assert.IsTrue(service.IsManagedSkillPath(result.RelativeDirectoryPath));

                service.DeleteManagedSkillDirectory(result.RelativeDirectoryPath);

                Assert.IsFalse(Directory.Exists(savedDirectory));
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(savedDirectory) && Directory.Exists(savedDirectory))
                {
                    Directory.Delete(savedDirectory, recursive: true);
                }
            }
        }

        [TestMethod]
        public void AgentManagedSkillsService_UsesFrontMatterNameForGenericSkillUploads()
        {
            var agentId = Guid.NewGuid();
            string? savedDirectory = null;

            try
            {
                var environment = new TestEnvironment { ContentRootPath = Path.GetTempPath() };
                var service = new AgentManagedSkillsService(environment, NullLogger<AgentManagedSkillsService>.Instance);
                const string existingSkill = "---\nname: release-notes-helper\ndescription: 'Custom skill.'\nargument-hint: 'Ask for help.'\n---\n\n# Release Notes Helper\n\nUse the existing content.";

                var result = service.CreateManagedSkill(agentId, "SKILL", existingSkill, "SKILL.md");
                savedDirectory = Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar));

                Assert.AreEqual("release-notes-helper", result.DisplayName);
                StringAssert.Contains(result.RelativeDirectoryPath, "release-notes-helper-");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(savedDirectory) && Directory.Exists(savedDirectory))
                {
                    Directory.Delete(savedDirectory, recursive: true);
                }
            }
        }

        private sealed class TestEnvironment : IWebHostEnvironment
        {
            public string ApplicationName { get; set; } = "Egroo.Server.Test";
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get; set; } = string.Empty;
            public string EnvironmentName { get; set; } = "Development";
            public string ContentRootPath { get; set; } = string.Empty;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        }
    }
}