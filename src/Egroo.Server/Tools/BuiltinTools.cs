using Microsoft.Extensions.AI;

namespace Egroo.Server.Tools
{
    /// <summary>
    /// Aggregates built-in tool groups into a single catalog used by the API and runtime.
    /// </summary>
    public static class BuiltinTools
    {
        public static List<BuiltinToolDefinition> GetDefinitions()
        {
            var definitions = new List<BuiltinToolDefinition>();
            definitions.AddRange(TimeTool.GetDefinitions());
            definitions.AddRange(WebTool.GetDefinitions());
            definitions.AddRange(A2ATool.GetDefinitions());
            definitions.AddRange(EgrooTool.GetDefinitions());
            return definitions;
        }

        public static IList<AITool> CreateTools()
        {
            var tools = new List<AITool>();
            tools.AddRange(TimeTool.CreateTools());
            tools.AddRange(WebTool.CreateTools());
            return tools;
        }

        public static IList<AITool> CreateScopedTools(IServiceScopeFactory scopeFactory, Guid callerUserId)
        {
            var tools = new List<AITool>();
            tools.AddRange(A2ATool.CreateScopedTools(scopeFactory, callerUserId));
            tools.AddRange(EgrooTool.CreateScopedTools(scopeFactory, callerUserId));
            return tools;
        }

        public static IList<AITool> CreateTools(IEnumerable<string> enabledToolNames)
        {
            var enabled = new HashSet<string>(enabledToolNames, StringComparer.OrdinalIgnoreCase);
            return CreateTools()
                .Where(t => t is AIFunction f && enabled.Contains(f.Name))
                .ToList();
        }

        public static ISet<string> GetScopedToolNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            names.UnionWith(A2ATool.ScopedToolNames);
            names.UnionWith(EgrooTool.ScopedToolNames);
            return names;
        }

        internal static IReadOnlyList<WebSearchResult> ParseDuckDuckGoResults(string html, int maxResults) => WebTool.ParseDuckDuckGoResults(html, maxResults);

        internal static WebPageContent ExtractReadableContentFromHtml(string html, int maxChars) => WebTool.ExtractReadableContentFromHtml(html, maxChars);

        internal static Task<string?> ValidatePublicInternetUrlAsync(Uri uri) => WebTool.ValidatePublicInternetUrlAsync(uri);
    }
}