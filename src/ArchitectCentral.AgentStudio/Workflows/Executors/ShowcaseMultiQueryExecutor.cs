using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.RAG;
using ArchitectCentral.AgentStudio.Services;
using Microsoft.Agents.AI.Workflows;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 2: Multi-Query RAG - Executes parallel architecture queries
/// </summary>
public sealed class ShowcaseMultiQueryExecutor : Executor
{
    private readonly IRAGQueryService _ragService;
    private readonly AppSettings _settings;

    public ShowcaseMultiQueryExecutor(IRAGQueryService ragService, AppSettings settings) 
        : base("ShowcaseMultiQuery")
    {
        _ragService = ragService;
        _settings = settings;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseDiscoveryResult, ShowcaseMultiQueryResult>(HandleRequestAsync))
            .YieldsOutput<ShowcaseMultiQueryResult>();
    }

    public async ValueTask<ShowcaseMultiQueryResult> HandleRequestAsync(
        ShowcaseDiscoveryResult discovery,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n🔎 Stage 2: Multi-Query RAG (4 parallel queries)");
        
        var queries = new[]
        {
            $"Architecture patterns for {discovery.Codebase}",
            $"Best practices for {discovery.ArtifactType}",
            $"Design patterns for {discovery.Description}",
            "Agent generation guidelines"
        };

        var tasks = queries.Select(q => _ragService.QueryAsync(q, maxResults: 5)).ToArray();
        var results = await Task.WhenAll(tasks);

        var allChunks = results.SelectMany(r => r).ToList();
        Console.WriteLine($"   Retrieved {allChunks.Count} knowledge chunks");

        return new ShowcaseMultiQueryResult
        {
            Discovery = discovery,
            KnowledgeChunks = allChunks
        };
    }
}

public record ShowcaseMultiQueryResult
{
    public required ShowcaseDiscoveryResult Discovery { get; init; }
    public required List<RAGResult> KnowledgeChunks { get; init; }
}
