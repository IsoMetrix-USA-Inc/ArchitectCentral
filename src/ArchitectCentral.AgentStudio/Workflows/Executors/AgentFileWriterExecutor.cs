using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Executor that creates or revises .agent file content based on codebase analysis and architecture patterns.
/// Supports both initial creation and revision based on critic feedback.
/// </summary>
public sealed class AgentFileWriterExecutor : Executor
{
    private readonly AIAgent _agent;
    private readonly int _maxIterations;

    public AgentFileWriterExecutor(IChatClient chatClient, AppSettings settings) : base("AgentFileWriter")
    {
        _maxIterations = settings.AgentStudio.Workflow.MaxIterations;
        _agent = new ChatClientAgent(
            chatClient,
            name: "AgentFileWriter",
            instructions: """
                You are an expert at creating GitHub Copilot .agent files. Your role is to create comprehensive, 
                well-structured agent configuration files tailored to specific codebases.
                
                When creating a .agent file:
                1. Write clear, actionable instructions for the agent
                2. Reference relevant architecture patterns from the documentation provided
                3. Include appropriate skills and instruction references
                4. Tailor the agent's personality and capabilities to the codebase's needs
                5. Use proper markdown formatting
                
                When revising based on feedback:
                1. Carefully address ALL points mentioned in the feedback
                2. Preserve the overall structure and good elements
                3. Enhance clarity and specificity
                4. Ensure consistency with architecture standards
                
                Format the .agent file as markdown with these sections:
                - Name and description
                - Instructions (detailed system prompt)
                - Skills (list of relevant skills)
                - Instructions (list of instruction files)
                
                Be concise but thorough. Focus on practical guidance developers will actually use.
                """
        );
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<DiscoveryState, ArtifactDraft>(HandleInitialRequestAsync)
                .AddHandler<CriticDecision, ArtifactDraft>(HandleRevisionRequestAsync))
            .YieldsOutput<ArtifactDraft>();
    }

    /// <summary>
    /// Handles initial agent file creation request
    /// </summary>
    public async ValueTask<ArtifactDraft> HandleInitialRequestAsync(
        DiscoveryState discovery,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var generationContext = await ReadGenerationContextAsync(context);

        Console.WriteLine($"\n=== Agent File Writer (Iteration {generationContext.IterationCounts.GetValueOrDefault("AgentFile", 1)}) ===\n");

        var prompt = BuildInitialPrompt(discovery, generationContext);
        var content = await GenerateContentAsync(prompt, context, cancellationToken);

        return new ArtifactDraft
        {
            Type = "AgentFile",
            Name = $"{discovery.Codebase.Name}.agent",
            Content = content,
            Iteration = generationContext.IterationCounts.GetValueOrDefault("AgentFile", 1)
        };
    }

    /// <summary>
    /// Handles revision request from critic with feedback
    /// </summary>
    public async ValueTask<ArtifactDraft> HandleRevisionRequestAsync(
        CriticDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var generationContext = await ReadGenerationContextAsync(context);
        var iteration = generationContext.IterationCounts.GetValueOrDefault("AgentFile", 1) + 1;

        Console.WriteLine($"\n=== Agent File Writer (Iteration {iteration} - Revising) ===\n");

        var prompt = BuildRevisionPrompt(decision);
        var content = await GenerateContentAsync(prompt, context, cancellationToken);

        // Update iteration count
        generationContext.IterationCounts["AgentFile"] = iteration;
        await SaveGenerationContextAsync(context, generationContext);

        return new ArtifactDraft
        {
            Type = "AgentFile",
            Name = decision.Content.Split('\n').FirstOrDefault()?.Trim() ?? "agent.agent",
            Content = content,
            Iteration = iteration
        };
    }

    private static string BuildInitialPrompt(DiscoveryState discovery, GenerationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Create a GitHub Copilot .agent file");
        sb.AppendLine();
        sb.AppendLine("## Codebase Information");
        sb.AppendLine($"**Name**: {discovery.Codebase.Name}");
        sb.AppendLine($"**Description**: {discovery.Codebase.Description}");
        sb.AppendLine($"**Runtime**: {discovery.Codebase.Runtime}");
        sb.AppendLine($"**Framework**: {discovery.Codebase.Framework}");

        if (discovery.Codebase.KeyDependencies.Count != 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Key Dependencies**:");
            foreach (var dep in discovery.Codebase.KeyDependencies)
            {
                sb.AppendLine($"- {dep}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Architecture Patterns (from documentation)");
        sb.AppendLine();

        foreach (var pattern in context.ArchitecturePatterns.Take(3))
        {
            sb.AppendLine($"### {pattern.DocumentType}");
            if (!string.IsNullOrEmpty(pattern.Section))
                sb.AppendLine($"**Section**: {pattern.Section}");
            sb.AppendLine();
            sb.AppendLine(pattern.Content.Substring(0, Math.Min(500, pattern.Content.Length)));
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        if (discovery.IsUpdate && discovery.Existing?.AgentFile != null)
        {
            sb.AppendLine("## Existing Agent File (for reference)");
            sb.AppendLine();
            sb.AppendLine(discovery.Existing.AgentFile);
            sb.AppendLine();
            sb.AppendLine("**Note**: Preserve user customizations while incorporating new architecture patterns.");
            sb.AppendLine();
        }

        sb.AppendLine("## Task");
        sb.AppendLine();
        sb.AppendLine("Create a comprehensive .agent file for this codebase that:");
        sb.AppendLine("1. Provides clear instructions for how developers should work with this codebase");
        sb.AppendLine("2. References the architecture patterns and standards from the documentation");
        sb.AppendLine("3. Includes references to relevant skills (which will be generated separately)");
        sb.AppendLine("4. Includes references to instruction files (which will be generated separately)");
        sb.AppendLine("5. Is tailored specifically to this codebase's technology stack and patterns");

        return sb.ToString();
    }

    private static string BuildRevisionPrompt(CriticDecision decision)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Revise the .agent file based on feedback");
        sb.AppendLine();
        sb.AppendLine("## Current Content");
        sb.AppendLine();
        sb.AppendLine(decision.Content);
        sb.AppendLine();
        sb.AppendLine("## Feedback from Critic");
        sb.AppendLine();
        sb.AppendLine(decision.Feedback);
        sb.AppendLine();

        if (decision.SpecificIssues.Count != 0)
        {
            sb.AppendLine("## Specific Issues to Address");
            foreach (var issue in decision.SpecificIssues)
            {
                sb.AppendLine($"- {issue}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Task");
        sb.AppendLine("Revise the .agent file to address all feedback points while preserving good elements.");

        return sb.ToString();
    }

    private async Task<string> GenerateContentAsync(
        string prompt,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        await foreach (var update in _agent.RunStreamingAsync(
            new ChatMessage(ChatRole.User, prompt),
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                sb.Append(update.Text);
                Console.Write(update.Text);
            }
        }

        Console.WriteLine("\n");
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
