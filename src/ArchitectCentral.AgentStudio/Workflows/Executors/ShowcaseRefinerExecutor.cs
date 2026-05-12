using ArchitectCentral.AgentStudio.Configuration;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 6: Refiner - Addresses feedback and loops back to critic
/// </summary>
public sealed class ShowcaseRefinerExecutor : Executor
{
    private readonly IChatClient _chatClient;
    private readonly AppSettings _settings;

    public ShowcaseRefinerExecutor(IChatClient chatClient, AppSettings settings) 
        : base("ShowcaseRefiner")
    {
        _chatClient = chatClient;
        _settings = settings;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseCriticDecision, ShowcaseRefinerDecision>(HandleRequestAsync))
            .YieldsOutput<ShowcaseRefinerDecision>();
    }

    public async ValueTask<ShowcaseRefinerDecision> HandleRequestAsync(
        ShowcaseCriticDecision criticDecision,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n🔧 Stage 6: Refinement (preparing iteration {criticDecision.CurrentIteration + 1})");
        
        // Package the feedback for the writer
        return new ShowcaseRefinerDecision
        {
            PreviousContent = criticDecision.Draft.Content,
            Feedback = criticDecision.Feedback,
            CurrentIteration = criticDecision.CurrentIteration,
            Discovery = criticDecision.Draft.Discovery
        };
    }
}

public record ShowcaseRefinerDecision
{
    public required string PreviousContent { get; init; }
    public required string Feedback { get; init; }
    public required int CurrentIteration { get; init; }
    public required ShowcaseDiscoveryResult Discovery { get; init; }
}
