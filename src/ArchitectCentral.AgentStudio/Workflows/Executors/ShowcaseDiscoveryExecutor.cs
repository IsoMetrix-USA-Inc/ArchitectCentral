using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Showcase;
using ArchitectCentral.AgentStudio.Services;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Stage 1: Discovery - Analyzes codebase and extracts requirements
/// </summary>
public sealed class ShowcaseDiscoveryExecutor : Executor
{
    private readonly IChatClient _chatClient;
    private readonly IRAGQueryService _ragService;
    private readonly AppSettings _settings;

    public ShowcaseDiscoveryExecutor(IChatClient chatClient, IRAGQueryService ragService, AppSettings settings)
        : base("ShowcaseDiscovery")
    {
        _chatClient = chatClient;
        _ragService = ragService;
        _settings = settings;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<ShowcaseWorkflowRequest, ShowcaseDiscoveryResult>(HandleRequestAsync))
            .YieldsOutput<ShowcaseDiscoveryResult>();
    }

    public async ValueTask<ShowcaseDiscoveryResult> HandleRequestAsync(
        ShowcaseWorkflowRequest request,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("\n🔍 Stage 1: Discovery & Analysis");
            Console.WriteLine($"   Codebase: {request.CodebaseName}");
            Console.WriteLine($"   Description: {request.Description}");
            Console.WriteLine($"   ArtifactType: {request.ArtifactType}");

            return await ValueTask.FromResult(new ShowcaseDiscoveryResult
            {
                Codebase = request.CodebaseName,
                Description = request.Description,
                ArtifactType = request.ArtifactType,
                ExtractedRequirements = ["Generate comprehensive agent", "Include best practices", "Reference architecture patterns"]
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERROR in Discovery: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            throw;
        }
    }
}

public record ShowcaseDiscoveryResult
{
    public required string Codebase { get; init; }
    public required string Description { get; init; }
    public required string ArtifactType { get; init; }
    public required string[] ExtractedRequirements { get; init; }
}
