using ArchitectCentral.AgentStudio.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 4: Writer - Generates artifact with context
/// </summary>
public sealed class ShowcaseWriterExecutor : Executor
{
    private readonly AIAgent _agent;
    private readonly AppSettings _settings;

    public ShowcaseWriterExecutor(IChatClient chatClient, AppSettings settings) 
        : base("ShowcaseWriter")
    {
        _settings = settings;
        _agent = new ChatClientAgent(
            chatClient,
            name: "ShowcaseWriter",
            instructions: """
                You are an expert at creating GitHub Copilot .agent files.
                Generate comprehensive, well-structured agent configuration files.
                Include clear instructions, relevant skills, and actionable guidance.
                Use proper markdown formatting with frontmatter.
                """);
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseSynthesisResult, ShowcaseArtifactDraft>(HandleInitialRequestAsync)
                .AddHandler<ShowcaseRefinerDecision, ShowcaseArtifactDraft>(HandleRevisionRequestAsync))
            .YieldsOutput<ShowcaseArtifactDraft>();
    }

    public async ValueTask<ShowcaseArtifactDraft> HandleInitialRequestAsync(
        ShowcaseSynthesisResult synthesis,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n✍️  Stage 4: Generation (Iteration 1)");
        
        var knowledgeContext = string.Join("\n\n", synthesis.SynthesizedKnowledge.Select(k => k.Content));
        var prompt = $"""
            Create a .agent file for: {synthesis.Discovery.Codebase}
            Description: {synthesis.Discovery.Description}
            
            Architecture Patterns:
            {knowledgeContext}
            """;

        var result = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var content = result.ToString();

        Console.WriteLine($"   Generated {content.Length} characters");

        return new ShowcaseArtifactDraft
        {
            Content = content,
            Iteration = 1,
            Discovery = synthesis.Discovery
        };
    }

    public async ValueTask<ShowcaseArtifactDraft> HandleRevisionRequestAsync(
        ShowcaseRefinerDecision decision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n✍️  Stage 4: Generation (Iteration {decision.CurrentIteration + 1})");
        
        var prompt = $"""
            Revise the following .agent file based on feedback:
            
            Previous Content:
            {decision.PreviousContent}
            
            Feedback:
            {decision.Feedback}
            
            Generate an improved version addressing all feedback points.
            """;

        var result = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var content = result.ToString();

        Console.WriteLine($"   Generated {content.Length} characters");

        return new ShowcaseArtifactDraft
        {
            Content = content,
            Iteration = decision.CurrentIteration + 1,
            Discovery = decision.Discovery
        };
    }
}

public record ShowcaseArtifactDraft
{
    public required string Content { get; init; }
    public required int Iteration { get; init; }
    public required ShowcaseDiscoveryResult Discovery { get; init; }
}
