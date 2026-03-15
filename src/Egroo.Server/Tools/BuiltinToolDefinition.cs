using System.Diagnostics.CodeAnalysis;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Definition of a built-in tool for seeding/display purposes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class BuiltinToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ParametersSchema { get; set; }
    }
}