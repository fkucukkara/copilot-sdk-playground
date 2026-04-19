using System.ComponentModel;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace CopilotSDKPlayground.Api.Services;

/// <summary>
/// Demonstrates how to register custom tools with a Copilot session using
/// <see cref="AIFunctionFactory.Create"/> from <c>Microsoft.Extensions.AI</c>.
/// When Copilot decides to call a tool, the SDK runs the handler automatically
/// and feeds the result back to the model.
/// </summary>
public class ToolCopilotService
{
    private readonly ICopilotService _copilotService;
    private readonly ILogger<ToolCopilotService> _logger;

    public static readonly IReadOnlyList<string> AvailableToolNames = ["get_weather", "calculate", "get_current_time"];

    public ToolCopilotService(ICopilotService copilotService, ILogger<ToolCopilotService> logger)
    {
        _copilotService = copilotService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a session with all demo tools pre-registered.
    /// The <paramref name="model"/> must support tool/function calling (e.g. gpt-4o).
    /// </summary>
    public Task<CopilotSession> CreateToolSessionAsync(string model, string? sessionId = null) =>
        _copilotService.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SessionId = sessionId,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Tools = [BuildWeatherTool(), BuildCalculatorTool(), BuildTimeTool()]
        });

    // ── Tool definitions ─────────────────────────────────────────────────────
    // Local methods are used instead of lambdas because:
    //   1. [Description] attributes on lambda parameters cause delegate type inference failure.
    //   2. Branches returning different anonymous types require an explicit 'object' return type.

    private AIFunction BuildWeatherTool()
    {
        object GetWeather(
            [Description("City name, e.g. 'London'")] string city,
            [Description("Temperature unit: 'celsius' or 'fahrenheit'")] string unit = "celsius")
        {
            _logger.LogInformation("Tool: get_weather({City}, {Unit})", city, unit);
            var temps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["london"] = 14, ["new york"] = 22, ["tokyo"] = 26,
                ["berlin"] = 11, ["sydney"] = 19, ["istanbul"] = 18
            };
            var tempC = temps.TryGetValue(city, out var t) ? t : 20;
            var temp = unit.Equals("fahrenheit", StringComparison.OrdinalIgnoreCase)
                ? tempC * 9 / 5 + 32 : tempC;
            return new { city, temperature = temp, unit, condition = "Partly cloudy", humidity = "65%" };
        }

        return AIFunctionFactory.Create(GetWeather,
            name: "get_weather",
            description: "Returns the current weather for a city. Always use this tool when the user asks about weather.");
    }

    private static AIFunction BuildCalculatorTool()
    {
        static object Calculate(
            [Description("A math expression, e.g. '(12 + 4) * 3 / 2'")] string expression)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(expression, @"^[\d\s\+\-\*\/\(\)\.]+$"))
                return new { error = "Expression contains unsupported characters." };

            try
            {
                var result = new System.Data.DataTable().Compute(expression, null);
                return new { expression, result };
            }
            catch (Exception ex)
            {
                return new { expression, error = ex.Message };
            }
        }

        return AIFunctionFactory.Create(Calculate,
            name: "calculate",
            description: "Evaluates a mathematical expression. Use this for arithmetic calculations.");
    }

    private static AIFunction BuildTimeTool()
    {
        static object GetTime(
            [Description("IANA timezone, e.g. 'Europe/London', 'America/New_York'. Leave empty for UTC.")] string? timezone = null)
        {
            TimeZoneInfo tz;
            string tzLabel;
            try
            {
                tz = string.IsNullOrWhiteSpace(timezone)
                    ? TimeZoneInfo.Utc
                    : TimeZoneInfo.FindSystemTimeZoneById(timezone);
                tzLabel = tz.DisplayName;
            }
            catch
            {
                tz = TimeZoneInfo.Utc;
                tzLabel = "UTC (fallback — timezone not found)";
            }
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return new { timezone = tzLabel, localTime = localTime.ToString("O"), utcOffset = tz.BaseUtcOffset.ToString() };
        }

        return AIFunctionFactory.Create(GetTime,
            name: "get_current_time",
            description: "Returns the current date and time for a given timezone.");
    }
}
