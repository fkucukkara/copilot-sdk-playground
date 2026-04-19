using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CopilotSDKPlayground.McpServer.Tools;

/// <summary>
/// MCP tool that returns mock weather data for a given city.
/// Demonstrates basic MCP tool authoring with structured parameters.
/// </summary>
[McpServerToolType]
public class WeatherTool
{
    private static readonly Dictionary<string, (double Celsius, string Condition)> MockData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["London"]     = (14.5, "Cloudy"),
        ["New York"]   = (22.3, "Sunny"),
        ["Tokyo"]      = (28.1, "Humid"),
        ["Sydney"]     = (18.7, "Partly Cloudy"),
        ["Berlin"]     = (12.0, "Rainy"),
        ["Paris"]      = (16.4, "Overcast"),
        ["Dubai"]      = (38.5, "Sunny"),
        ["Istanbul"]   = (20.2, "Clear"),
        ["San Francisco"] = (17.8, "Foggy"),
        ["Singapore"]  = (31.0, "Thunderstorm"),
    };

    [McpServerTool(Name = "get_weather")]
    [Description("Returns the current weather for a city. Supports Celsius or Fahrenheit.")]
    public static string GetWeather(
        [Description("City name, e.g. 'London'")] string city,
        [Description("Temperature unit: 'celsius' or 'fahrenheit'")] string unit = "celsius")
    {
        var (celsius, condition) = MockData.GetValueOrDefault(city, (20.0, "Clear"));

        var temp = unit.Equals("fahrenheit", StringComparison.OrdinalIgnoreCase)
            ? Math.Round(celsius * 9 / 5 + 32, 1)
            : celsius;

        var symbol = unit.Equals("fahrenheit", StringComparison.OrdinalIgnoreCase) ? "°F" : "°C";
        return $"Weather in {city}: {temp}{symbol}, {condition}. (Data is simulated for demo purposes.)";
    }
}
