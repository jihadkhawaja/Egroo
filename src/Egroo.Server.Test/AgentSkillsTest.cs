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

        // ── CreateContextProviders ──────────────────────────────────────────────

        [TestMethod]
        public void CreateContextProviders_NoEnabledSkills_ReturnsEmpty()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var result = service.CreateContextProviders(Array.Empty<AgentSkillDirectory>(), null);
                Assert.AreEqual(0, result.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void CreateContextProviders_WithSkillsAndPlaceholderPrompt_ReturnsProvider()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            var skillDir = Path.Combine(tempRoot, "my-skill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# Test skill");
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var result = service.CreateContextProviders(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "My Skill", Path = "my-skill", IsEnabled = true }
                }, "Available skills:\n{0}");

                Assert.AreEqual(1, result.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void CreateContextProviders_PromptWithoutPlaceholder_StillReturnsProvider()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            var skillDir = Path.Combine(tempRoot, "my-skill2");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# Test");
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var result = service.CreateContextProviders(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Skill", Path = "my-skill2", IsEnabled = true }
                }, "No placeholder here");

                Assert.AreEqual(1, result.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void CreateContextProviders_NullPrompt_ReturnsProvider()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            var skillDir = Path.Combine(tempRoot, "my-skill3");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "# Test");
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var result = service.CreateContextProviders(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Skill", Path = "my-skill3", IsEnabled = true }
                }, null);

                Assert.AreEqual(1, result.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        // ── ResolveSkillPaths edge cases ────────────────────────────────────────────

        [TestMethod]
        public void ResolveSkillPaths_DisabledDirectory_IsExcluded()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            var skillDir = Path.Combine(tempRoot, "disabled-skill");
            Directory.CreateDirectory(skillDir);
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var resolved = service.ResolveSkillPaths(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Disabled", Path = "disabled-skill", IsEnabled = false }
                });

                Assert.AreEqual(0, resolved.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void ResolveSkillPaths_EmptyPath_IsExcluded()
        {
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var env = new TestEnvironment { ContentRootPath = Path.GetTempPath() };
            var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

            var resolved = service.ResolveSkillPaths(new[]
            {
                new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Empty", Path = "", IsEnabled = true },
                new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Whitespace", Path = "   ", IsEnabled = true }
            });

            Assert.AreEqual(0, resolved.Count);
        }

        [TestMethod]
        public void ResolveSkillPaths_DuplicatePaths_AreDeduped()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            var skillDir = Path.Combine(tempRoot, "shared-skill");
            Directory.CreateDirectory(skillDir);
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var resolved = service.ResolveSkillPaths(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Skill A", Path = "shared-skill", IsEnabled = true },
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Skill B", Path = "shared-skill", IsEnabled = true }
                });

                Assert.AreEqual(1, resolved.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void ResolveSkillPaths_NonexistentDirectory_IsExcluded()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["AgentSkills:AllowedRootPaths:0"] = tempRoot })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var resolved = service.ResolveSkillPaths(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Absent", Path = "this-does-not-exist", IsEnabled = true }
                });

                Assert.AreEqual(0, resolved.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void ResolveSkillPaths_NoAllowedRoots_AcceptsAnyValidPath()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "egroo-skills-test", Guid.NewGuid().ToString("N"));
            var skillDir = Path.Combine(tempRoot, "any-skill");
            Directory.CreateDirectory(skillDir);
            try
            {
                // Empty config = no AllowedRootPaths restriction
                IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection().Build();
                var env = new TestEnvironment { ContentRootPath = tempRoot };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var resolved = service.ResolveSkillPaths(new[]
                {
                    new AgentSkillDirectory { AgentDefinitionId = Guid.NewGuid(), Name = "Any", Path = "any-skill", IsEnabled = true }
                });

                Assert.AreEqual(1, resolved.Count);
            }
            finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
        }

        [TestMethod]
        public void ResolveSkillPaths_SkillsPrefix_UsesRunLocationBase()
        {
            // Create a skills directory under AppContext.BaseDirectory (run location)
            var skillDir = Path.Combine(AppContext.BaseDirectory, "skills", "test-skill-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(skillDir);
            try
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AgentSkills:AllowedRootPaths:0"] = Path.Combine(AppContext.BaseDirectory, "skills")
                    })
                    .Build();
                var env = new TestEnvironment { ContentRootPath = Path.GetTempPath() };
                var service = new AgentSkillsService(config, env, NullLoggerFactory.Instance, NullLogger<AgentSkillsService>.Instance);

                var resolved = service.ResolveSkillPaths(new[]
                {
                    new AgentSkillDirectory
                    {
                        AgentDefinitionId = Guid.NewGuid(),
                        Name = "Run Skill",
                        Path = "skills/" + Path.GetFileName(skillDir),
                        IsEnabled = true
                    }
                });

                Assert.AreEqual(1, resolved.Count);
                StringAssert.Contains(resolved[0], "skills");
            }
            finally { if (Directory.Exists(skillDir)) Directory.Delete(skillDir, true); }
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

        // ── AgentManagedSkillsService branch tests ──────────────────────────────────

        private static AgentManagedSkillsService CreateManagedService()
        {
            var env = new TestEnvironment { ContentRootPath = Path.GetTempPath() };
            return new AgentManagedSkillsService(env, NullLogger<AgentManagedSkillsService>.Instance);
        }

        [TestMethod]
        public void ManagedSkills_EmptyContent_Throws()
        {
            var svc = CreateManagedService();
            Assert.ThrowsExactly<ArgumentException>(() => svc.CreateManagedSkill(Guid.NewGuid(), "name", ""));
        }

        [TestMethod]
        public void ManagedSkills_WhitespaceContent_Throws()
        {
            var svc = CreateManagedService();
            Assert.ThrowsExactly<ArgumentException>(() => svc.CreateManagedSkill(Guid.NewGuid(), "name", "   "));
        }

        [TestMethod]
        public void ManagedSkills_OversizedContent_Throws()
        {
            var svc = CreateManagedService();
            var bigContent = new string('x', 256 * 1024 + 1);
            Assert.ThrowsExactly<ArgumentException>(() => svc.CreateManagedSkill(Guid.NewGuid(), "name", bigContent));
        }

        [TestMethod]
        public void ManagedSkills_IsManagedSkillPath_NullOrEmpty_ReturnsFalse()
        {
            var svc = CreateManagedService();
            Assert.IsFalse(svc.IsManagedSkillPath(null));
            Assert.IsFalse(svc.IsManagedSkillPath(""));
            Assert.IsFalse(svc.IsManagedSkillPath("   "));
        }

        [TestMethod]
        public void ManagedSkills_IsManagedSkillPath_NonManagedPath_ReturnsFalse()
        {
            var svc = CreateManagedService();
            Assert.IsFalse(svc.IsManagedSkillPath("some/other/path"));
        }

        [TestMethod]
        public void ManagedSkills_IsManagedSkillPath_ValidPath_ReturnsTrue()
        {
            var svc = CreateManagedService();
            Assert.IsTrue(svc.IsManagedSkillPath("skills/managed/agents/something"));
        }

        [TestMethod]
        public void ManagedSkills_DeleteManagedSkillDirectory_NonManaged_NoOp()
        {
            var svc = CreateManagedService();
            // Should not throw for non-managed path — just returns
            svc.DeleteManagedSkillDirectory("not/a/managed/path");
        }

        [TestMethod]
        public void ManagedSkills_DeleteManagedSkillDirectory_NonExistentDir_NoOp()
        {
            var svc = CreateManagedService();
            svc.DeleteManagedSkillDirectory("skills/managed/agents/nonexistent-" + Guid.NewGuid().ToString("N"));
        }

        [TestMethod]
        public void ManagedSkills_CreateWithContentHeading_UsesHeadingName()
        {
            var svc = CreateManagedService();
            var agentId = Guid.NewGuid();
            ManagedSkillFile? result = null;
            try
            {
                result = svc.CreateManagedSkill(agentId, "skill", "# My Cool Skill\n\nContent here");
                Assert.AreEqual("My Cool Skill", result.DisplayName);
            }
            finally
            {
                if (result is not null)
                {
                    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void ManagedSkills_CreateWithFrontMatterName_UsesFrontMatterName()
        {
            var svc = CreateManagedService();
            var agentId = Guid.NewGuid();
            ManagedSkillFile? result = null;
            try
            {
                var content = "---\nname: my-front-skill\ndescription: test\n---\n\n# Heading\n\nBody";
                result = svc.CreateManagedSkill(agentId, "skill", content);
                Assert.AreEqual("my-front-skill", result.DisplayName);
            }
            finally
            {
                if (result is not null)
                {
                    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void ManagedSkills_CreateWithFileName_UsesFileName()
        {
            var svc = CreateManagedService();
            var agentId = Guid.NewGuid();
            ManagedSkillFile? result = null;
            try
            {
                result = svc.CreateManagedSkill(agentId, "", "Some content", "my-skill-file.md");
                Assert.AreEqual("my skill file", result.DisplayName);
            }
            finally
            {
                if (result is not null)
                {
                    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void ManagedSkills_CreateWithFrontMatterEmptyName_FallsBackToSkillName()
        {
            var svc = CreateManagedService();
            var agentId = Guid.NewGuid();
            ManagedSkillFile? result = null;
            try
            {
                var content = "---\nname:   \n---\n\nBody text";
                result = svc.CreateManagedSkill(agentId, "MySkill", content);
                Assert.AreEqual("MySkill", result.DisplayName);
            }
            finally
            {
                if (result is not null)
                {
                    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void ManagedSkills_CreateWithFrontMatterNoNameField_FallsBackToSkillName()
        {
            var svc = CreateManagedService();
            var agentId = Guid.NewGuid();
            ManagedSkillFile? result = null;
            try
            {
                var content = "---\ndescription: only desc\n---\n\nBody text";
                result = svc.CreateManagedSkill(agentId, "FallbackSkill", content);
                Assert.AreEqual("FallbackSkill", result.DisplayName);
            }
            finally
            {
                if (result is not null)
                {
                    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void ManagedSkills_CreateWithNoHeadingNoFrontMatter_UsesSkillName()
        {
            var svc = CreateManagedService();
            var agentId = Guid.NewGuid();
            ManagedSkillFile? result = null;
            try
            {
                result = svc.CreateManagedSkill(agentId, "DirectName", "Just plain body text without any heading");
                Assert.AreEqual("DirectName", result.DisplayName);
            }
            finally
            {
                if (result is not null)
                {
                    var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, result.RelativeDirectoryPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }
    }
}