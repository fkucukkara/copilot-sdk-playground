using CopilotSDKPlayground.Api.Models;
using CopilotSDKPlayground.Api.Services;
using GitHub.Copilot.SDK;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CopilotSDKPlayground.Api.Endpoints;

/// <summary>
/// Demonstrates how different system prompts and few-shot examples change
/// agent behaviour on identical user inputs.
/// Uses <see cref="SystemMessageConfig"/> with <see cref="SystemMessageMode.Replace"/>
/// so the template fully controls the system prompt.
/// </summary>
public static class PromptTemplateEndpoints
{
    public static IEndpointRouteBuilder MapPromptTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prompts").WithTags("Prompt Templates");

        group.MapGet("/templates", ListTemplates)
             .WithSummary("List all built-in prompt templates");

        group.MapGet("/templates/{name}", GetTemplate)
             .WithSummary("Get a specific prompt template by name");

        group.MapPost("/sessions", CreateTemplateSession)
             .WithSummary("Create a chat session pre-configured with a prompt template");

        return app;
    }

    private static Ok<List<PromptTemplate>> ListTemplates(PromptTemplateService templateService) =>
        TypedResults.Ok(templateService.All.Values.ToList());

    private static Results<Ok<PromptTemplate>, NotFound> GetTemplate(
        string name,
        PromptTemplateService templateService)
    {
        var template = templateService.Get(name);
        return template is null ? TypedResults.NotFound() : TypedResults.Ok(template);
    }

    private static async Task<Results<Created<TemplateSessionResponse>, NotFound<string>>> CreateTemplateSession(
        CreateTemplateSessionRequest request,
        ICopilotService copilotService,
        PromptTemplateService templateService,
        IConfiguration configuration)
    {
        var template = templateService.Get(request.TemplateName);
        if (template is null)
        {
            return TypedResults.NotFound($"Template '{request.TemplateName}' not found. " +
                $"Available: {string.Join(", ", templateService.All.Keys)}");
        }

        var model = request.Model ?? configuration.GetValue<string>("CopilotSDK:DefaultModel", "gpt-4o")!;
        var enrichedPrompt = BuildEnrichedPrompt(template);

        // Use SDK SystemMessage config — cleaner than sending the system prompt as a user message
        var session = await copilotService.CreateSessionAsync(new SessionConfig
        {
            Model = model,
            SessionId = request.SessionId,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = enrichedPrompt
            }
        });

        var response = new TemplateSessionResponse
        {
            SessionId = session.SessionId,
            TemplateName = template.Name,
            SystemPromptPreview = template.SystemPrompt[..Math.Min(120, template.SystemPrompt.Length)] + "…",
            FewShotExampleCount = template.FewShotExamples.Count
        };

        return TypedResults.Created($"/api/chat/{session.SessionId}", response);
    }

    private static string BuildEnrichedPrompt(PromptTemplate template)
    {
        if (template.FewShotExamples.Count == 0)
            return template.SystemPrompt;

        var examples = template.FewShotExamples
            .Select(e => $"User: {e.User}\nAssistant: {e.Assistant}")
            .Aggregate((a, b) => $"{a}\n\n{b}");

        return $"{template.SystemPrompt}\n\nExamples:\n{examples}";
    }
}
