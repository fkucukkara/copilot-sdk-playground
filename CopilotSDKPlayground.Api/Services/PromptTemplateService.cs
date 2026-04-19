using CopilotSDKPlayground.Api.Models;

namespace CopilotSDKPlayground.Api.Services;

/// <summary>
/// Provides built-in prompt templates that demonstrate how system prompts and
/// few-shot examples shape Copilot behaviour on identical user inputs.
/// </summary>
public class PromptTemplateService
{
    private static readonly Dictionary<string, PromptTemplate> _templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CodeReviewer"] = new PromptTemplate
        {
            Name = "CodeReviewer",
            Description = "Senior code reviewer — focuses on correctness, security, and maintainability",
            SystemPrompt =
                "You are a senior software engineer conducting a thorough code review. " +
                "Identify bugs, security vulnerabilities (OWASP Top 10), performance issues, and style violations. " +
                "Structure your response as: ISSUES (critical/major/minor) followed by SUGGESTIONS. " +
                "Be direct and actionable. Cite line numbers when possible.",
            FewShotExamples =
            [
                new FewShotExample
                {
                    User = "Review: `var query = \"SELECT * FROM users WHERE id = \" + userId;`",
                    Assistant = "CRITICAL — SQL Injection (OWASP A03:2021): String concatenation in SQL allows an attacker to manipulate the query. Fix: use parameterised queries — `WHERE id = @userId`."
                }
            ]
        },
        ["TechnicalWriter"] = new PromptTemplate
        {
            Name = "TechnicalWriter",
            Description = "Technical writer — transforms code and concepts into clear documentation",
            SystemPrompt =
                "You are an expert technical writer specialising in developer documentation. " +
                "Write concise, accurate, and jargon-appropriate content for a developer audience. " +
                "Use Markdown. Prefer short paragraphs and concrete examples over abstract descriptions. " +
                "Always include a code example when documenting an API or function.",
            FewShotExamples =
            [
                new FewShotExample
                {
                    User = "Document the CreateSession endpoint.",
                    Assistant = "## POST /api/chat/sessions\n\nCreates a new Copilot chat session.\n\n```http\nPOST /api/chat/sessions\nContent-Type: application/json\n\n{ \"model\": \"gpt-4o\" }\n```\n\n**Response** `201 Created` — returns `sessionId` used for subsequent message calls."
                }
            ]
        },
        ["SocraticTutor"] = new PromptTemplate
        {
            Name = "SocraticTutor",
            Description = "Socratic tutor — guides learners to answers through questions",
            SystemPrompt =
                "You are a Socratic tutor. Never give direct answers — instead guide the learner through targeted questions that help them discover the solution themselves. " +
                "Ask one question at a time. Acknowledge correct reasoning with brief praise before moving to the next question. " +
                "If the learner is completely stuck after three attempts, give a small hint (not the full answer).",
            FewShotExamples = []
        },
        ["DataAnalyst"] = new PromptTemplate
        {
            Name = "DataAnalyst",
            Description = "Data analyst — interprets data, suggests visualisations, and writes analytical summaries",
            SystemPrompt =
                "You are a senior data analyst. When given data or a description of a dataset: " +
                "1) Summarise key statistics. 2) Identify trends, outliers, or anomalies. " +
                "3) Suggest appropriate visualisations (chart type + axes). 4) Recommend next analytical steps. " +
                "Use bullet points. Be quantitative where possible.",
            FewShotExamples = []
        }
    };

    public IReadOnlyDictionary<string, PromptTemplate> All => _templates;

    public PromptTemplate? Get(string name) =>
        _templates.TryGetValue(name, out var t) ? t : null;
}
