using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.RAG;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 3: Knowledge Synthesis - Ranks and combines patterns
/// </summary>
public sealed class ShowcaseSynthesisExecutor : Executor
{
    private readonly IChatClient _chatClient;
    private readonly AppSettings _settings;

    public ShowcaseSynthesisExecutor(IChatClient chatClient, AppSettings settings) 
        : base("ShowcaseSynthesis")
    {
        _chatClient = chatClient;
        _settings = settings;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseMultiQueryResult, ShowcaseSynthesisResult>(HandleRequestAsync))
            .YieldsOutput<ShowcaseSynthesisResult>();
    }

    public async ValueTask<ShowcaseSynthesisResult> HandleRequestAsync(
        ShowcaseMultiQueryResult multiQuery,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n🧠 Stage 3: Knowledge Synthesis");
        
        var topChunks = multiQuery.KnowledgeChunks
            .OrderByDescending(c => c.Similarity)
            .Take(_settings.AgentStudio.Workflow.MaxKnowledgeChunks)
            .ToList();

        Console.WriteLine($"   Selected top {topChunks.Count} chunks for generation");

        return new ShowcaseSynthesisResult
        {
            Discovery = multiQuery.Discovery,
            SynthesizedKnowledge = topChunks
        };
    }
}

public record ShowcaseSynthesisResult
{
    public required ShowcaseDiscoveryResult Discovery { get; init; }
    public required List<RAGResult> SynthesizedKnowledge { get; init; }
}
