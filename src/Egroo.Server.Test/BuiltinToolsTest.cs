using Egroo.Server.Tools;
using Microsoft.Extensions.AI;

namespace Egroo.Server.Test
{
    [TestClass]
    public class BuiltinToolsTest
    {
        // ── GetDefinitions ──────────────────────────────────────────────────────────

        [TestMethod]
        public void GetDefinitions_ReturnsNonEmptyList()
        {
            var definitions = BuiltinTools.GetDefinitions();

            Assert.IsNotNull(definitions);
            Assert.IsTrue(definitions.Count > 0, "Expected at least one built-in tool definition.");
        }

        [TestMethod]
        public void GetDefinitions_AllHaveNameAndDescription()
        {
            var definitions = BuiltinTools.GetDefinitions();

            foreach (var def in definitions)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Name), "Tool name should not be empty.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Description), "Tool description should not be empty.");
            }
        }

        [TestMethod]
        public void GetDefinitions_ContainsExpectedToolNames()
        {
            var definitions = BuiltinTools.GetDefinitions();
            var names = definitions.Select(d => d.Name).ToList();

            CollectionAssert.Contains(names, "get_current_datetime");
            CollectionAssert.Contains(names, "convert_time_between_zones");
            CollectionAssert.Contains(names, "list_timezones");
            CollectionAssert.Contains(names, "search_published_agents");
            CollectionAssert.Contains(names, "add_agent_friend");
            CollectionAssert.Contains(names, "add_agent_to_channel");
            CollectionAssert.Contains(names, "discover_agent_friends");
            CollectionAssert.Contains(names, "message_agent_friend");
            CollectionAssert.Contains(names, "search_web");
            CollectionAssert.Contains(names, "fetch_web_page");
        }

        // ── CreateTools ─────────────────────────────────────────────────────────────

        [TestMethod]
        public void CreateTools_ReturnsNonEmptyList()
        {
            var tools = BuiltinTools.CreateTools();

            Assert.IsNotNull(tools);
            Assert.IsTrue(tools.Count > 0);
        }

        [TestMethod]
        public void CreateTools_ContainsDateTimeTool()
        {
            var tools = BuiltinTools.CreateTools();
            var dateTimeTool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == "get_current_datetime");

            Assert.IsNotNull(dateTimeTool, "Expected get_current_datetime tool.");
        }

        [TestMethod]
        public void CreateTools_ContainsConvertTimeTool()
        {
            var tools = BuiltinTools.CreateTools();
            var convertTool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == "convert_time_between_zones");

            Assert.IsNotNull(convertTool, "Expected convert_time_between_zones tool.");
        }

        [TestMethod]
        public void CreateTools_ContainsListTimezonesTool()
        {
            var tools = BuiltinTools.CreateTools();
            var listTool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == "list_timezones");

            Assert.IsNotNull(listTool, "Expected list_timezones tool.");
        }

        [TestMethod]
        public void CreateTools_ContainsSearchWebTool()
        {
            var tools = BuiltinTools.CreateTools();
            var searchTool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == "search_web");

            Assert.IsNotNull(searchTool, "Expected search_web tool.");
        }

        [TestMethod]
        public void CreateTools_ContainsFetchWebPageTool()
        {
            var tools = BuiltinTools.CreateTools();
            var fetchTool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == "fetch_web_page");

            Assert.IsNotNull(fetchTool, "Expected fetch_web_page tool.");
        }

        // ── CreateTools(filteredNames) ──────────────────────────────────────────────

        [TestMethod]
        public void CreateTools_WithFilteredNames_ReturnsOnlyMatchingTools()
        {
            var enabled = new[] { "get_current_datetime" };
            var tools = BuiltinTools.CreateTools(enabled);

            Assert.AreEqual(1, tools.Count);
            Assert.AreEqual("get_current_datetime", ((AIFunction)tools[0]).Name);
        }

        [TestMethod]
        public void CreateTools_WithNoMatchingNames_ReturnsEmptyList()
        {
            var tools = BuiltinTools.CreateTools(new[] { "nonexistent_tool" });

            Assert.AreEqual(0, tools.Count);
        }

        [TestMethod]
        public void CreateTools_WithMultipleMatchingNames_ReturnsAllMatches()
        {
            var enabled = new[] { "get_current_datetime", "list_timezones" };
            var tools = BuiltinTools.CreateTools(enabled);

            Assert.AreEqual(2, tools.Count);
        }

        // ── GetCurrentDateTime (invoke via AIFunction) ─────────────────────────────

        [TestMethod]
        public async Task GetCurrentDateTime_WithoutTimezone_ReturnsUtcInfo()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "get_current_datetime");

            var result = await tool.InvokeAsync(new AIFunctionArguments());

            Assert.IsNotNull(result);
            var output = result.ToString()!;
            StringAssert.Contains(output, "Current UTC:");
            StringAssert.Contains(output, "Day of week:");
            StringAssert.Contains(output, "ISO 8601:");
            StringAssert.Contains(output, "Unix timestamp:");
        }

        [TestMethod]
        public async Task GetCurrentDateTime_WithValidTimezone_ReturnsLocalTime()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "get_current_datetime");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["timezone"] = "UTC" }));

            Assert.IsNotNull(result);
            var output = result.ToString()!;
            StringAssert.Contains(output, "UTC");
        }

        [TestMethod]
        public async Task GetCurrentDateTime_WithInvalidTimezone_ReturnsNotFoundMessage()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "get_current_datetime");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["timezone"] = "Invalid/Timezone" }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "not found");
        }

        // ── ConvertTimeBetweenZones ────────────────────────────────────────────────

        [TestMethod]
        public async Task ConvertTimeBetweenZones_ValidInput_ReturnsConversion()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "convert_time_between_zones");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["datetime"] = "2025-03-04T15:00:00",
                ["from_timezone"] = "UTC",
                ["to_timezone"] = "UTC"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "Source:");
            StringAssert.Contains(result.ToString()!, "Target:");
        }

        [TestMethod]
        public async Task ConvertTimeBetweenZones_InvalidDatetime_ReturnsError()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "convert_time_between_zones");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["datetime"] = "not-a-date",
                ["from_timezone"] = "UTC",
                ["to_timezone"] = "UTC"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "Could not parse");
        }

        [TestMethod]
        public async Task ConvertTimeBetweenZones_InvalidTimezone_ReturnsError()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "convert_time_between_zones");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["datetime"] = "2025-03-04T15:00:00",
                ["from_timezone"] = "Invalid/Zone",
                ["to_timezone"] = "UTC"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "not found");
        }

        // ── ListTimezones ──────────────────────────────────────────────────────────

        [TestMethod]
        public async Task ListTimezones_WithoutSearch_ReturnsResults()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "list_timezones");

            var result = await tool.InvokeAsync(new AIFunctionArguments());

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "timezone");
        }

        [TestMethod]
        public async Task ListTimezones_WithSearch_FiltersResults()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "list_timezones");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["search"] = "UTC" }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "UTC");
        }

        [TestMethod]
        public async Task ListTimezones_WithNoMatches_ReturnsNoResults()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "list_timezones");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?> { ["search"] = "zzz_nonexistent_zzz" }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "No timezones found");
        }

        // ── Web Search / Fetch ─────────────────────────────────────────────────────

        [TestMethod]
        public async Task SearchWeb_WithoutQuery_ReturnsValidationError()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "search_web");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["query"] = ""
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "required");
        }

        [TestMethod]
        public async Task FetchWebPage_WithInvalidScheme_ReturnsValidationError()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "fetch_web_page");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["url"] = "file:///secret.txt"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "HTTP or HTTPS");
        }

        [TestMethod]
        public async Task FetchWebPage_WithLocalhost_ReturnsValidationError()
        {
            var tools = BuiltinTools.CreateTools();
            var tool = tools.OfType<AIFunction>().First(t => t.Name == "fetch_web_page");

            var result = await tool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["url"] = "http://localhost:8080"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "public internet URLs");
        }

        [TestMethod]
        public void ParseDuckDuckGoResults_ExtractsTitleUrlAndSnippet()
        {
            const string html = """
                                <html>
                                    <body>
                                        <div class="results">
                                            <a href="//duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com%2Farticle">Example Result</a>
                                            <div class="result-snippet">An example snippet about the article.</div>
                                        </div>
                                    </body>
                                </html>
                                """;

            var results = BuiltinTools.ParseDuckDuckGoResults(html, 5);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Example Result", results[0].Title);
            Assert.AreEqual("https://example.com/article", results[0].Url);
            StringAssert.Contains(results[0].Snippet, "example snippet");
        }

        [TestMethod]
        public void ExtractReadableContentFromHtml_StripsMarkupAndTruncates()
        {
            const string html = """
                                <html>
                                    <head>
                                        <title>Example Page</title>
                                        <style>.hidden { display:none; }</style>
                                    </head>
                                    <body>
                                        <script>console.log('ignore');</script>
                                        <h1>Headline</h1>
                                        <p>First paragraph.</p>
                                        <p>Second paragraph with <strong>markup</strong>.</p>
                                    </body>
                                </html>
                                """;

            var content = BuiltinTools.ExtractReadableContentFromHtml(html, 60);

            Assert.AreEqual("Example Page", content.Title);
            StringAssert.Contains(content.Content, "Headline");
            StringAssert.Contains(content.Content, "First paragraph");
            Assert.IsTrue(content.WasTruncated);
            Assert.IsFalse(content.Content.Contains("console.log", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task ValidatePublicInternetUrlAsync_RejectsPrivateIp()
        {
            var result = await BuiltinTools.ValidatePublicInternetUrlAsync(new Uri("http://192.168.1.10/test"));

            Assert.IsNotNull(result);
            StringAssert.Contains(result, "public internet URLs");
        }

        // ── CreateScopedTools ──────────────────────────────────────────────────────

        [TestMethod]
        public void CreateScopedTools_ReturnsExpectedToolCount()
        {
            var services = TestServiceProvider.Build(dbName: $"BuiltinScopedDb_{Guid.NewGuid():N}", authenticatedUserId: Guid.NewGuid());
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, Guid.NewGuid());

            Assert.IsNotNull(tools);
            Assert.AreEqual(5, tools.Count);
        }

        [TestMethod]
        public async Task DiscoverAgentFriends_NoFriends_ReturnsGuidance()
        {
            var dbName = $"BuiltinDiscoverFriendsDb_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var discoverTool = tools.OfType<AIFunction>().First(t => t.Name == "discover_agent_friends");

            var result = await discoverTool.InvokeAsync(new AIFunctionArguments());

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "No friend agents");
        }

        [TestMethod]
        public async Task MessageAgentFriend_InvalidGuid_ReturnsError()
        {
            var dbName = $"BuiltinMessageFriendDb_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var messageTool = tools.OfType<AIFunction>().First(t => t.Name == "message_agent_friend");

            var result = await messageTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["agent_id"] = "bad-guid",
                ["message"] = "hello"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "Invalid agent ID");
        }

        [TestMethod]
        public async Task MessageAgentFriend_EmptyMessage_ReturnsError()
        {
            var dbName = $"BuiltinMessageFriendDb2_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var messageTool = tools.OfType<AIFunction>().First(t => t.Name == "message_agent_friend");

            var result = await messageTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["agent_id"] = Guid.NewGuid().ToString(),
                ["message"] = ""
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "Message content is required");
        }

        [TestMethod]
        public async Task SearchPublishedAgents_NoMatches_ReturnsNotFoundMessage()
        {
            var dbName = $"BuiltinScopedSearchDb_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var searchTool = tools.OfType<AIFunction>().First(t => t.Name == "search_published_agents");

            var result = await searchTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["query"] = "nonexistent",
                ["max_results"] = 10
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "No published agents found");
        }

        [TestMethod]
        public async Task AddAgentFriend_InvalidGuid_ReturnsError()
        {
            var dbName = $"BuiltinAddFriendDb_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var addFriendTool = tools.OfType<AIFunction>().First(t => t.Name == "add_agent_friend");

            var result = await addFriendTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["agent_id"] = "not-a-guid"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "Invalid agent ID");
        }

        [TestMethod]
        public async Task AddAgentFriend_NonexistentAgent_ReturnsNotFound()
        {
            var dbName = $"BuiltinAddFriendDb2_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var addFriendTool = tools.OfType<AIFunction>().First(t => t.Name == "add_agent_friend");

            var result = await addFriendTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["agent_id"] = Guid.NewGuid().ToString()
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "not found");
        }

        [TestMethod]
        public async Task AddAgentToChannel_InvalidIds_ReturnsError()
        {
            var dbName = $"BuiltinAddChanDb_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var addTool = tools.OfType<AIFunction>().First(t => t.Name == "add_agent_to_channel");

            var result = await addTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["channel_id"] = "bad",
                ["agent_id"] = "bad"
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "Invalid ID");
        }

        [TestMethod]
        public async Task AddAgentToChannel_NotAdmin_ReturnsError()
        {
            var dbName = $"BuiltinAddChanDb2_{Guid.NewGuid():N}";
            var userId = Guid.NewGuid();
            var services = TestServiceProvider.Build(dbName: dbName, authenticatedUserId: userId);
            var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var tools = BuiltinTools.CreateScopedTools(scopeFactory, userId);
            var addTool = tools.OfType<AIFunction>().First(t => t.Name == "add_agent_to_channel");

            var result = await addTool.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["channel_id"] = Guid.NewGuid().ToString(),
                ["agent_id"] = Guid.NewGuid().ToString()
            }));

            Assert.IsNotNull(result);
            StringAssert.Contains(result.ToString()!, "admin");
        }
    }
}
