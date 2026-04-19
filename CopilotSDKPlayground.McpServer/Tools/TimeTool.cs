using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CopilotSDKPlayground.McpServer.Tools;

/// <summary>
/// MCP tool that returns the current date and time, optionally in a given IANA timezone.
/// </summary>
[McpServerToolType]
public class TimeTool
{
    [McpServerTool(Name = "get_current_time")]
    [Description("Returns the current date and time. Optionally converts to an IANA timezone like 'America/New_York' or 'Europe/London'.")]
    public static string GetCurrentTime(
        [Description("IANA timezone ID, e.g. 'UTC', 'America/New_York', 'Europe/Istanbul'. Defaults to UTC.")] string timezone = "UTC")
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return $"Current time in {timezone}: {now:yyyy-MM-dd HH:mm:ss} ({tz.DisplayName})";
        }
        catch (TimeZoneNotFoundException)
        {
            return $"Unknown timezone '{timezone}'. Using UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
        }
    }
}
