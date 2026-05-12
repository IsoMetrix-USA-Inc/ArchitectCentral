using ArchitectCentral.AgentStudio.Configuration;
using Microsoft.Agents.AI.Workflows;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 7: Finalizer - Packages output with metadata
/// </summary>
public sealed class ShowcaseFinalizerExecutor : Executor
{
    private readonly AppSettings _settings;

    public ShowcaseFinalizerExecutor(AppSettings settings) 
        : base("ShowcaseFinalizer")
    {
        _settings = settings;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseCriticDecision, ShowcaseFinalResult>(HandleRequestAsync))
            .YieldsOutput<ShowcaseFinalResult>();
    }

    public async ValueTask<ShowcaseFinalResult> HandleRequestAsync(
        ShowcaseCriticDecision criticDecision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n📦 Stage 7: Finalization");
        
        var result = new ShowcaseFinalResult
        {
            Content = criticDecision.Draft.Content,
            FinalScore = criticDecision.OverallScore,
            Iterations = criticDecision.CurrentIteration,
            FileName = $"{criticDecision.Draft.Discovery.Codebase}.agent",
            Approved = criticDecision.Approved
        };

        Console.WriteLine($"\n✅ Workflow Complete!");
        Console.WriteLine($"   Final Score: {result.FinalScore:F2}");
        Console.WriteLine($"   Iterations: {result.Iterations}");
        Console.WriteLine($"   File: {result.FileName}");

        return await ValueTask.FromResult(result);
    }
}

public record ShowcaseFinalResult
{
    public required string Content { get; init; }
    public required double FinalScore { get; init; }
    public required int Iterations { get; init; }
    public required string FileName { get; init; }
    public required bool Approved { get; init; }
}
