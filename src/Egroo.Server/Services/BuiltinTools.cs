using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Egroo.Server.Services
{
    /// <summary>
    /// Provides built-in AI tools that are always available to agents.
    /// These are real executable functions wrapped as AITool instances.
    /// </summary>
    public static class BuiltinTools
    {
        /// <summary>
        /// Returns all built-in tool definitions (for seeding into the DB).
        /// </summary>
        public static List<BuiltinToolDefinition> GetDefinitions() =>
        [
            new()
            {
                Name = "get_current_datetime",
                Description = "Get the current date and time in UTC and optionally in a specific timezone. Use this when the user asks about the current time, date, day of the week, or any temporal information.",
                ParametersSchema = """{"type":"object","properties":{"timezone":{"type":"string","description":"Optional IANA timezone name (e.g. 'America/New_York', 'Europe/London', 'Asia/Tokyo'). If not provided, returns UTC."}},"required":[]}"""
            },
            new()
            {
                Name = "convert_time_between_zones",
                Description = "Convert a specific date/time from one timezone to another. Useful when the user asks 'what time is it in Tokyo when it's 3pm in New York?' or similar.",
                ParametersSchema = """{"type":"object","properties":{"datetime":{"type":"string","description":"The datetime to convert in ISO 8601 format (e.g. '2025-03-04T15:00:00')"},"from_timezone":{"type":"string","description":"Source IANA timezone name (e.g. 'America/New_York')"},"to_timezone":{"type":"string","description":"Target IANA timezone name (e.g. 'Asia/Tokyo')"}},"required":["datetime","from_timezone","to_timezone"]}"""
            },
            new()
            {
                Name = "list_timezones",
                Description = "List available timezone names for a given region or search term. Use this to help the user find the correct timezone identifier.",
                ParametersSchema = """{"type":"object","properties":{"search":{"type":"string","description":"Optional search term to filter timezones (e.g. 'America', 'Europe', 'Pacific')"}},"required":[]}"""
            }
        ];

        /// <summary>
        /// Creates the actual AITool instances that can be invoked by the AI agent.
        /// </summary>
        public static IList<AITool> CreateTools()
        {
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(GetCurrentDateTime, "get_current_datetime",
                    "Get the current date and time in UTC and optionally in a specific timezone."),

                AIFunctionFactory.Create(ConvertTimeBetweenZones, "convert_time_between_zones",
                    "Convert a specific date/time from one timezone to another."),

                AIFunctionFactory.Create(ListTimezones, "list_timezones",
                    "List available timezone names for a given region or search term.")
            };

            return tools;
        }

        /// <summary>
        /// Returns a subset of built-in tools filtered by the enabled tool names.
        /// </summary>
        public static IList<AITool> CreateTools(IEnumerable<string> enabledToolNames)
        {
            var enabled = new HashSet<string>(enabledToolNames, StringComparer.OrdinalIgnoreCase);
            var all = CreateTools();
            return all.Where(t => t is AIFunction f && enabled.Contains(f.Name)).ToList();
        }

        // ── Tool Implementations ─────────────────────────────────────

        [Description("Get the current date and time in UTC and optionally in a specific timezone.")]
        private static string GetCurrentDateTime(
            [Description("Optional IANA timezone name (e.g. 'America/New_York', 'Europe/London', 'Asia/Tokyo'). If not provided, returns UTC.")]
            string? timezone = null)
        {
            var utcNow = DateTimeOffset.UtcNow;
            var sb = new StringBuilder();

            sb.AppendLine($"Current UTC: {utcNow:yyyy-MM-dd HH:mm:ss} (UTC)");
            sb.AppendLine($"Day of week: {utcNow.DayOfWeek}");
            sb.AppendLine($"ISO 8601: {utcNow:O}");
            sb.AppendLine($"Unix timestamp: {utcNow.ToUnixTimeSeconds()}");

            if (!string.IsNullOrWhiteSpace(timezone))
            {
                try
                {
                    var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                    var local = TimeZoneInfo.ConvertTime(utcNow, tz);
                    sb.AppendLine();
                    sb.AppendLine($"In {tz.DisplayName} ({timezone}):");
                    sb.AppendLine($"  Date/Time: {local:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine($"  Day of week: {local.DayOfWeek}");
                    sb.AppendLine($"  UTC offset: {tz.GetUtcOffset(utcNow)}");
                    sb.AppendLine($"  Is daylight saving: {tz.IsDaylightSavingTime(utcNow)}");
                }
                catch (TimeZoneNotFoundException)
                {
                    sb.AppendLine($"\nTimezone '{timezone}' not found. Use list_timezones to find valid timezone names.");
                }
            }

            return sb.ToString();
        }

        [Description("Convert a specific date/time from one timezone to another.")]
        private static string ConvertTimeBetweenZones(
            [Description("The datetime to convert in ISO 8601 format (e.g. '2025-03-04T15:00:00')")]
            string datetime,
            [Description("Source IANA timezone name (e.g. 'America/New_York')")]
            string from_timezone,
            [Description("Target IANA timezone name (e.g. 'Asia/Tokyo')")]
            string to_timezone)
        {
            try
            {
                if (!DateTimeOffset.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    // Try as DateTime without offset
                    if (!DateTime.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        return $"Could not parse datetime '{datetime}'. Please use ISO 8601 format (e.g. '2025-03-04T15:00:00').";
                    }
                    parsed = new DateTimeOffset(dt, TimeSpan.Zero);
                }

                var fromTz = TimeZoneInfo.FindSystemTimeZoneById(from_timezone);
                var toTz = TimeZoneInfo.FindSystemTimeZoneById(to_timezone);

                // Interpret the parsed time as being in the source timezone
                var sourceTime = new DateTimeOffset(parsed.DateTime, fromTz.GetUtcOffset(parsed.DateTime));
                var targetTime = TimeZoneInfo.ConvertTime(sourceTime, toTz);

                var sb = new StringBuilder();
                sb.AppendLine($"Source: {sourceTime:yyyy-MM-dd HH:mm:ss} ({from_timezone}, UTC{fromTz.GetUtcOffset(sourceTime):hh\\:mm})");
                sb.AppendLine($"Target: {targetTime:yyyy-MM-dd HH:mm:ss} ({to_timezone}, UTC{toTz.GetUtcOffset(targetTime):hh\\:mm})");
                sb.AppendLine($"Time difference: {toTz.GetUtcOffset(targetTime) - fromTz.GetUtcOffset(sourceTime)} hours");

                if (sourceTime.Date != targetTime.Date)
                {
                    sb.AppendLine($"Note: The date changes during conversion ({sourceTime:ddd MMM dd} → {targetTime:ddd MMM dd}).");
                }

                return sb.ToString();
            }
            catch (TimeZoneNotFoundException ex)
            {
                return $"Timezone not found: {ex.Message}. Use list_timezones to find valid timezone names.";
            }
        }

        [Description("List available timezone names for a given region or search term.")]
        private static string ListTimezones(
            [Description("Optional search term to filter timezones (e.g. 'America', 'Europe', 'Pacific')")]
            string? search = null)
        {
            var zones = TimeZoneInfo.GetSystemTimeZones()
                .AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                zones = zones.Where(z =>
                    z.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    z.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var results = zones
                .Select(z => new
                {
                    Id = z.Id,
                    Display = z.DisplayName,
                    Offset = z.BaseUtcOffset.ToString(@"hh\:mm")
                })
                .Take(30)
                .ToList();

            if (results.Count == 0)
            {
                return $"No timezones found matching '{search}'. Try broader terms like 'US', 'Europe', 'Asia'.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {results.Count} timezone(s):");
            foreach (var z in results)
            {
                sb.AppendLine($"  {z.Id} — {z.Display}");
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Definition of a built-in tool for seeding/display purposes.
    /// </summary>
    public class BuiltinToolDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ParametersSchema { get; set; }
    }
}
