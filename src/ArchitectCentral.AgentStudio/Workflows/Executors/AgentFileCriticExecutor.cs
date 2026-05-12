using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Executor that reviews .agent file content and decides whether to approve or request revisions.
/// Uses structured output for reliable decision-making.
/// </summary>
public sealed class AgentFileCriticExecutor : Executor<ArtifactDraft, CriticDecision>
{
    private readonly AIAgent _agent;
    private readonly int _maxIterations;

    public AgentFileCriticExecutor(IChatClient chatClient, AppSettings settings) : base("AgentFileCritic")
    {
        _maxIterations = settings.AgentStudio.Workflow.MaxIterations;
        _agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "AgentFileCritic",
            ChatOptions = new()
            {
                Instructions = """
                    You are a meticulous reviewer of GitHub Copilot .agent files. Your role is to ensure 
                    that agent configurations are high-quality, clear, and aligned with architecture standards.
                    
                    Evaluate the .agent file on these criteria:
                    
                    **Structure & Format** (20%)
                    - Proper markdown formatting
                    - Clear sections (name, description, instructions, skills, instruction references)
                    - Well-organized content
                    
                    **Content Quality** (30%)
                    - Clear, actionable instructions for developers
                    - Specific guidance (not generic)
                    - Appropriate level of detail
                    
                    **Architecture Alignment** (30%)
                    - References relevant architecture patterns
                    - Consistent with organizational standards
                    - Incorporates architecture documentation appropriately
                    
                    **Tailoring** (20%)
                    - Specific to the codebase's technology stack
                    - Addresses the codebase's unique needs
                    - Relevant skills and instructions referenced
                    
                    **Scoring**:
                    - 0.9-1.0: Excellent, approve immediately
                    - 0.7-0.89: Good but has minor issues, provide specific feedback
                    - Below 0.7: Needs significant improvement, provide detailed feedback
                    
                    Be constructive and specific in your feedback. Identify concrete improvements.
                    Only approve if the content is genuinely high quality - be thorough, not lenient.
                    
                    Provide your decision as structured JSON output with:
                    - approved: true if quality score >= 0.8 and no critical issues, false otherwise
                    - quality_score: numeric score 0.0-1.0
                    - feedback: specific improvements needed (empty if approved)
                    - specific_issues: array of concrete problems to fix (empty if approved)
                    """,
                ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<CriticDecision>()
            }
        });
    }

    public override async ValueTask<CriticDecision> HandleAsync(
        ArtifactDraft draft,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var generationContext = await ReadGenerationContextAsync(context);

        Console.WriteLine($"=== Agent File Critic (Iteration {draft.Iteration}) ===\n");

        var prompt = BuildReviewPrompt(draft, generationContext);

        // Stream the response for visibility
        var updates = _agent.RunStreamingAsync(
            new ChatMessage(ChatRole.User, prompt),
            cancellationToken: cancellationToken);

        await foreach (var update in updates)
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                Console.Write(update.Text);
            }
        }
        Console.WriteLine("\n");

        // Convert stream to response and deserialize structured output
        var response = await updates.ToAgentResponseAsync(cancellationToken);
        var decision = JsonSerializer.Deserialize<CriticDecision>(
            response.Text,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new JsonException("Failed to deserialize CriticDecision from response");

        Console.WriteLine($"Quality Score: {decision.QualityScore:P0}");
        Console.WriteLine($"Decision: {(decision.Approved ? "[APPROVED]" : "[NEEDS REVISION]")}");

        if (!string.IsNullOrEmpty(decision.Feedback))
        {
            Console.WriteLine($"\nFeedback: {decision.Feedback}");
        }

        if (decision.SpecificIssues.Count != 0)
        {
            Console.WriteLine("\nSpecific Issues:");
            foreach (var issue in decision.SpecificIssues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }
        Console.WriteLine();

        // Safety: auto-approve if max iterations reached
        if (!decision.Approved && draft.Iteration >= _maxIterations)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] Max iterations ({_maxIterations}) reached - auto-approving");
            Console.ResetColor();
            decision.Approved = true;
            decision.Feedback = string.Empty;
            decision.SpecificIssues.Clear();
        }

        // Store quality metrics
        generationContext.QualityMetrics["AgentFile"] = decision.QualityScore;
        await SaveGenerationContextAsync(context, generationContext);

        // Populate workflow routing fields
        decision.Content = draft.Content;
        decision.Iteration = draft.Iteration;
        decision.ArtifactType = "AgentFile";

        return decision;
    }

    private string BuildReviewPrompt(ArtifactDraft draft, GenerationContext context)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Review this .agent file");
        sb.AppendLine();
        sb.AppendLine("## Content to Review");
        sb.AppendLine();
        sb.AppendLine(draft.Content);
        sb.AppendLine();
        sb.AppendLine("## Codebase Context");
        sb.AppendLine($"**Name**: {context.CodebaseInfo.Name}");
        sb.AppendLine($"**Runtime**: {context.CodebaseInfo.Runtime}");
        sb.AppendLine($"**Framework**: {context.CodebaseInfo.Framework}");
        sb.AppendLine();

        if (context.ArchitecturePatterns.Count != 0)
        {
            sb.AppendLine("## Architecture Patterns (for reference)");
            foreach (var pattern in context.ArchitecturePatterns.Take(2))
            {
                sb.AppendLine($"- {pattern.DocumentType}: {pattern.Section ?? "General"}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Task");
        sb.AppendLine("Evaluate this .agent file against the criteria and provide your structured decision.");
        sb.AppendLine($"This is iteration {draft.Iteration} of {_maxIterations}.");

        return sb.ToString();
    }

    // Helper methods for shared state management

    private async Task<GenerationContext> ReadGenerationContextAsync(IWorkflowContext context)
    {
        var state = await context.ReadStateAsync<GenerationContext>(
            GenerationContext.Key,
            scopeName: GenerationContext.Scope);
        return state ?? new GenerationContext();
    }

    private static ValueTask SaveGenerationContextAsync(IWorkflowContext context, GenerationContext state)
    {
        return context.QueueStateUpdateAsync(
            GenerationContext.Key,
            state,
            scopeName: GenerationContext.Scope);
    }
}
