using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Workflow;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 5: Critic - Evaluates quality with 6-dimension rubric
/// </summary>
public sealed class ShowcaseCriticExecutor : Executor
{
    private readonly AIAgent _agent;
    private readonly AppSettings _settings;

    public ShowcaseCriticExecutor(IChatClient chatClient, AppSettings settings) 
        : base("ShowcaseCritic")
    {
        _settings = settings;
        _agent = new ChatClientAgent(
            chatClient,
            name: "ShowcaseCritic",
            instructions: """
                You are a thorough artifact reviewer. Evaluate using a 6-dimension rubric:
                1. Completeness (0.0-1.0)
                2. Accuracy (0.0-1.0)
                3. Clarity (0.0-1.0)
                4. Consistency (0.0-1.0)
                5. Actionability (0.0-1.0)
                6. Tailoring (0.0-1.0)
                
                Respond in format:
                COMPLETENESS: [score]
                ACCURACY: [score]
                CLARITY: [score]
                CONSISTENCY: [score]
                ACTIONABILITY: [score]
                TAILORING: [score]
                OVERALL: [weighted average]
                APPROVED: [YES if >= 0.80, NO otherwise]
                FEEDBACK: [specific improvements needed]
                """);
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseArtifactDraft, ShowcaseCriticDecision>(HandleRequestAsync))
            .YieldsOutput<ShowcaseCriticDecision>();
    }

    public async ValueTask<ShowcaseCriticDecision> HandleRequestAsync(
        ShowcaseArtifactDraft draft,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n🎯 Stage 5: Critique (Iteration {draft.Iteration})");
        
        // Auto-approve after max iterations
        if (draft.Iteration >= _settings.AgentStudio.Workflow.MaxIterations)
        {
            Console.WriteLine($"   ⚠️  Max iterations reached - auto-approving");
            return new ShowcaseCriticDecision
            {
                Approved = true,
                OverallScore = 0.80,
                Feedback = "Auto-approved after maximum iterations",
                CurrentIteration = draft.Iteration,
                Draft = draft
            };
        }

        var prompt = $"""
            Evaluate this .agent file:
            
            {draft.Content}
            
            Provide scores for all 6 dimensions and determine if it meets the quality threshold of 0.80.
            """;

        var result = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        var critique = result.ToString();

        // Parse critique (simplified - in production would use structured output)
        var approved = critique.Contains("APPROVED: YES", StringComparison.OrdinalIgnoreCase);
        var overallScore = ParseScore(critique, "OVERALL");

        Console.WriteLine($"   Score: {overallScore:F2} | Approved: {approved}");

        return new ShowcaseCriticDecision
        {
            Approved = approved,
            OverallScore = overallScore,
            Feedback = critique,
            CurrentIteration = draft.Iteration,
            Draft = draft
        };
    }

    private static double ParseScore(string text, string dimension)
    {
        var pattern = $"{dimension}:\\s*([0-9.]+)";
        var match = System.Text.RegularExpressions.Regex.Match(text, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups[1].Value, out var score) ? score : 0.75;
    }
}

public record ShowcaseCriticDecision
{
    public required bool Approved { get; init; }
    public required double OverallScore { get; init; }
    public required string Feedback { get; init; }
    public required int CurrentIteration { get; init; }
    public required ShowcaseArtifactDraft Draft { get; init; }
}
