using ArchitectCentral.AgentStudio.Models.RAG;
using ArchitectCentral.AgentStudio.Models.Synthesis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Synthesizes raw RAG results into organized knowledge graph
/// </summary>
public class KnowledgeSynthesizerService
{
    private readonly ILogger<KnowledgeSynthesizerService> _logger;
    private const double SIMILARITY_THRESHOLD = 0.9;

    public KnowledgeSynthesizerService(ILogger<KnowledgeSynthesizerService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Synthesize multiple RAG result sets into organized knowledge graph
    /// </summary>
    public async Task<SynthesisResult> SynthesizeAsync(
        List<RAGResult> acceptedPatterns,
        List<RAGResult> codeExamples,
        List<RAGResult> antiPatterns,
        List<RAGResult> relatedTech,
        List<RAGResult> generalDocs)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Starting knowledge synthesis...");

        var graph = new KnowledgeGraph();
        var allResults = new List<RAGResult>();
        allResults.AddRange(acceptedPatterns);
        allResults.AddRange(codeExamples);
        allResults.AddRange(antiPatterns);
        allResults.AddRange(relatedTech);
        allResults.AddRange(generalDocs);

        var inputCount = allResults.Count;
        var duplicates = 0;

        // Deduplicate similar chunks across all results
        var uniqueResults = await DeduplicateChunksAsync(allResults);
        duplicates = inputCount - uniqueResults.Count;

        _logger.LogInformation("Deduplicated {DupCount} similar chunks ({UniqueCount} unique)",
            duplicates, uniqueResults.Count);

        // Categorize and organize into knowledge graph
        await CategorizeKnowledgeAsync(uniqueResults, graph, acceptedPatterns, antiPatterns, codeExamples);

        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        var result = new SynthesisResult
        {
            KnowledgeGraph = graph,
            InputChunkCount = inputCount,
            OutputItemCount = graph.TotalItems,
            DuplicatesMerged = duplicates,
            SynthesisTimeMs = elapsedMs,
            Summary = $"Synthesized {inputCount} chunks into {graph.TotalItems} knowledge items " +
                     $"({duplicates} duplicates merged) in {elapsedMs:F0}ms"
        };

        _logger.LogInformation("{Summary}", result.Summary);
        return result;
    }

    /// <summary>
    /// Remove duplicate/similar chunks using semantic similarity
    /// </summary>
    private static async Task<List<RAGResult>> DeduplicateChunksAsync(List<RAGResult> results)
    {
        if (results.Count == 0) return results;

        var unique = new List<RAGResult>();
        var seen = new HashSet<string>();

        // Simple deduplication by chunk_id and content similarity
        foreach (var result in results.OrderByDescending(r => r.Similarity))
        {
            if (seen.Contains(result.ChunkId))
                continue;

            // Check if very similar content already exists
            var isSimilar = unique.Any(u =>
                ComputeSimpleTextSimilarity(u.Content, result.Content) > SIMILARITY_THRESHOLD);

            if (!isSimilar)
            {
                unique.Add(result);
                seen.Add(result.ChunkId);
            }
        }

        return await Task.FromResult(unique);
    }

    /// <summary>
    /// Simple Jaccard similarity for text comparison
    /// </summary>
    private static double ComputeSimpleTextSimilarity(string text1, string text2)
    {
        var words1 = new HashSet<string>(
            Regex.Split(text1.ToLowerInvariant(), @"\W+").Where(w => w.Length > 3));
        var words2 = new HashSet<string>(
            Regex.Split(text2.ToLowerInvariant(), @"\W+").Where(w => w.Length > 3));

        if (words1.Count == 0 && words2.Count == 0) return 1.0;
        if (words1.Count == 0 || words2.Count == 0) return 0.0;

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union;
    }

    /// <summary>
    /// Categorize chunks into knowledge graph structure
    /// </summary>
    private async Task CategorizeKnowledgeAsync(
        List<RAGResult> uniqueResults,
        KnowledgeGraph graph,
        List<RAGResult> acceptedPatterns,
        List<RAGResult> antiPatterns,
        List<RAGResult> codeExamples)
    {
        foreach (var result in uniqueResults)
        {
            var item = MapToKnowledgeItem(result);

            // Categorize based on source query and content analysis
            if (antiPatterns.Any(ap => ap.ChunkId == result.ChunkId))
            {
                graph.AntiPatterns.Add(item);
            }
            else if (codeExamples.Any(ce => ce.ChunkId == result.ChunkId) || item.HasCodeExamples)
            {
                graph.Examples.Add(item);
            }
            else if (IsArchitecturalPrinciple(result))
            {
                graph.Principles.Add(item);
            }
            else if (IsDesignPattern(result))
            {
                graph.Patterns.Add(item);
            }
            else if (IsImplementationPractice(result))
            {
                graph.Practices.Add(item);
            }
            else if (IsTechnologyReference(result))
            {
                graph.RelatedTechnologies.Add(item);
            }
            else
            {
                // Default: treat as practice
                graph.Practices.Add(item);
            }
        }

        // Sort by relevance within each category
        graph.Principles = graph.Principles.OrderByDescending(i => i.RelevanceScore).ToList();
        graph.Patterns = graph.Patterns.OrderByDescending(i => i.RelevanceScore).ToList();
        graph.Practices = graph.Practices.OrderByDescending(i => i.RelevanceScore).ToList();
        graph.Examples = graph.Examples.OrderByDescending(i => i.RelevanceScore).ToList();
        graph.AntiPatterns = graph.AntiPatterns.OrderByDescending(i => i.RelevanceScore).ToList();
        graph.RelatedTechnologies = graph.RelatedTechnologies.OrderByDescending(i => i.RelevanceScore).ToList();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Map RAGResult to KnowledgeItem
    /// </summary>
    private KnowledgeItem MapToKnowledgeItem(RAGResult result)
    {
        var item = new KnowledgeItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = result.Section ?? "Architectural Guidance",
            Content = result.Content,
            DocumentType = result.DocumentType,
            Section = result.Section,
            RelevanceScore = result.Similarity,
            SourceChunkIds = new List<string> { result.ChunkId },
            MetadataJson = result.MetadataJson
        };

        // Parse metadata if available
        if (!string.IsNullOrWhiteSpace(result.MetadataJson))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result.MetadataJson);
                if (metadata != null)
                {
                    if (metadata.TryGetValue("quality_score", out var quality))
                        item.QualityScore = quality.GetDouble();

                    if (metadata.TryGetValue("sentiment", out var sentiment))
                        item.Sentiment = sentiment.GetString();

                    if (metadata.TryGetValue("technical_tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                        item.Tags = tags.EnumerateArray().Select(t => t.GetString() ?? "").ToList();

                    if (metadata.TryGetValue("framework_refs", out var frameworks) && frameworks.ValueKind == JsonValueKind.Array)
                        item.Frameworks = frameworks.EnumerateArray().Select(f => f.GetString() ?? "").ToList();

                    if (metadata.TryGetValue("has_code_examples", out var hasCode))
                        item.HasCodeExamples = hasCode.GetBoolean();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse metadata for chunk {ChunkId}", result.ChunkId);
            }
        }

        return item;
    }

    /// <summary>
    /// Detect if content describes an architectural principle
    /// </summary>
    private static bool IsArchitecturalPrinciple(RAGResult result)
    {
        var indicators = new[] { "principle", "philosophy", "approach", "strategy", "why we", "rationale", "decision" };
        var content = result.Content.ToLowerInvariant();
        return indicators.Any(indicator => content.Contains(indicator)) &&
               result.DocumentType.Equals("ADR", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detect if content describes a design pattern
    /// </summary>
    private static bool IsDesignPattern(RAGResult result)
    {
        var indicators = new[] { "pattern", "architecture", "structure", "design", "vertical slice", "cqrs",
                                "event sourcing", "microservice", "layered" };
        var content = result.Content.ToLowerInvariant();
        return indicators.Any(indicator => content.Contains(indicator));
    }

    /// <summary>
    /// Detect if content describes an implementation practice
    /// </summary>
    private static bool IsImplementationPractice(RAGResult result)
    {
        var indicators = new[] { "implement", "practice", "guideline", "convention", "standard",
                                "best practice", "how to", "step", "process" };
        var content = result.Content.ToLowerInvariant();
        return indicators.Any(indicator => content.Contains(indicator));
    }

    /// <summary>
    /// Detect if content references specific technologies
    /// </summary>
    private static bool IsTechnologyReference(RAGResult result)
    {
        var indicators = new[] { "framework", "library", "dependency", "package", "nuget", "npm",
                                "technology", "tool", "sdk", "api" };
        var content = result.Content.ToLowerInvariant();
        return indicators.Any(indicator => content.Contains(indicator));
    }
}
