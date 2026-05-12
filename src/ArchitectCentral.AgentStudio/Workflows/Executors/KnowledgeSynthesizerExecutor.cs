using ArchitectCentral.AgentStudio.Models.RAG;
using ArchitectCentral.AgentStudio.Models.Synthesis;
using ArchitectCentral.AgentStudio.Services;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ArchitectCentral.AgentStudio.Workflows.Executors;

/// <summary>
/// Executor that synthesizes RAG results into organized knowledge graph
/// </summary>
public sealed class KnowledgeSynthesizerExecutor : Executor
{
    private readonly KnowledgeSynthesizerService _synthesizer;
    private readonly ILogger<KnowledgeSynthesizerExecutor> _logger;

    public KnowledgeSynthesizerExecutor(
        KnowledgeSynthesizerService synthesizer,
        ILogger<KnowledgeSynthesizerExecutor> logger) 
        : base("KnowledgeSynthesizer")
    {
        _synthesizer = synthesizer;
        _logger = logger;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder)
    {
        return protocolBuilder
            .ConfigureRoutes(routeBuilder => routeBuilder
                .AddHandler<MultiQueryRAGData, SynthesisResult>(
                    (input, context, cancellationToken) => HandleSynthesisAsync(input)))
            .YieldsOutput<SynthesisResult>();
    }

    /// <summary>
    /// Handle synthesis request
    /// </summary>
    private async ValueTask<SynthesisResult> HandleSynthesisAsync(MultiQueryRAGData input)
    {
        _logger.LogInformation("Synthesizing knowledge from {Count} query result sets", 
            (input.AcceptedPatterns?.Count ?? 0) + 
            (input.CodeExamples?.Count ?? 0) + 
            (input.AntiPatterns?.Count ?? 0) + 
            (input.RelatedTech?.Count ?? 0) + 
            (input.GeneralDocs?.Count ?? 0));

        var result = await _synthesizer.SynthesizeAsync(
            input.AcceptedPatterns ?? new(),
            input.CodeExamples ?? new(),
            input.AntiPatterns ?? new(),
            input.RelatedTech ?? new(),
            input.GeneralDocs ?? new()
        );

        _logger.LogInformation("Synthesis complete: {Summary}", result.Summary);
        return result;
    }
}

/// <summary>
/// Input data containing multiple RAG query results
/// </summary>
public class MultiQueryRAGData
{
    public List<RAGResult>? AcceptedPatterns { get; set; }
    public List<RAGResult>? CodeExamples { get; set; }
    public List<RAGResult>? AntiPatterns { get; set; }
    public List<RAGResult>? RelatedTech { get; set; }
    public List<RAGResult>? GeneralDocs { get; set; }
}
