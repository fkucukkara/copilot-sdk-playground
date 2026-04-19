using System.ComponentModel;
using System.Data;
using ModelContextProtocol.Server;

namespace CopilotSDKPlayground.McpServer.Tools;

/// <summary>
/// MCP tool that safely evaluates simple arithmetic expressions.
/// Uses <see cref="DataTable.Compute"/> — no eval or code execution.
/// </summary>
[McpServerToolType]
public class CalculatorTool
{
    [McpServerTool(Name = "calculate")]
    [Description("Evaluates a basic arithmetic expression. Supports +, -, *, /, and parentheses. Example: '(3 + 4) * 2'")]
    public static string Calculate(
        [Description("Arithmetic expression to evaluate, e.g. '2 + 3 * 4'")] string expression)
    {
        try
        {
            // DataTable.Compute is a safe, built-in expression evaluator
            var result = new DataTable().Compute(expression, null);
            return $"Result: {result}";
        }
        catch (Exception ex)
        {
            return $"Error evaluating '{expression}': {ex.Message}";
        }
    }
}
