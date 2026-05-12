using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace KnowledgebaseVectoriser.Services.Metadata;

/// <summary>
/// Service for enriching document chunks with metadata
/// Extracts status, sentiment, quality scores, and categorization
/// </summary>
public class MetadataEnricherService
{
    private readonly ILogger<MetadataEnricherService> _logger;
    private readonly MetadataEnrichmentConfig _config;

    // Regex patterns for metadata extraction
    private static readonly Regex AdrStatusRegex = new(
        @"(?:^##?\s*Status\s*\n\s*\*\*([A-Za-z]+)\*\*)|(?:\*\*(Accepted|Proposed|Deprecated|Superseded)\*\*)|(?:Status:?\s*(Accepted|Proposed|Deprecated|Superseded))",
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled
    );

    // Sentiment patterns
    private static readonly Regex AntiPatternRegex = new(
        @"(?:anti[- ]pattern|don't|avoid|not recommended|bad practice|incorrect|wrong way|never|do not use)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex BestPracticeRegex = new(
        @"(?:best practice|recommended|should|correct|right way|prefer|good practice|always|use this)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex WarningRegex = new(
        @"(?:warning|caution|note|important|beware|be careful|security|vulnerability|risk)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // Category patterns
    private static readonly Dictionary<string, Regex> CategoryPatterns = new()
    {
        ["authentication"] = new Regex(@"(?:auth|login|sso|oauth|oidc|jwt|saml|identity|azure\s*(?:ad|b2c)|credential)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["data-architecture"] = new Regex(@"(?:database|postgres|sql|entity\s*framework|ef\s*core|repository|orm|migration|schema|data\s*access)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["api-design"] = new Regex(@"(?:api|rest|graphql|endpoint|http|web\s*service|controller|route|swagger|openapi)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["deployment"] = new Regex(@"(?:deploy|docker|kubernetes|k8s|container|azure|aws|cloud|infrastructure|terraform|ci/cd|pipeline)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["frontend"] = new Regex(@"(?:frontend|ui|react|angular|vue|blazor|typescript|javascript|css|html|spa)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["architecture-pattern"] = new Regex(@"(?:microservice|monolith|cqrs|event\s*sourcing|saga|clean\s*architecture|vertical\s*slice|ddd|domain\s*driven)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["testing"] = new Regex(@"(?:test|unit test|integration test|e2e|xunit|nunit|mock|stub|tdd|bdd|quality)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["security"] = new Regex(@"(?:security|encryption|certificate|ssl|tls|vulnerability|threat|cors|csrf|xss)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["observability"] = new Regex(@"(?:monitoring|logging|telemetry|metrics|trace|observability|application\s*insights|serilog|elk)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["performance"] = new Regex(@"(?:performance|optimization|cache|redis|cdn|latency|throughput|scalability|load)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Technical tag patterns
    private static readonly Dictionary<string, Regex> TechnicalTagPatterns = new()
    {
        ["architecture-patterns"] = new Regex(@"(?:cqrs|event\s*sourcing|saga|clean\s*architecture|vertical\s*slice|ddd|microservices|monolith)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["security"] = new Regex(@"(?:authentication|authorization|encryption|ssl|tls|oauth|jwt|certificate|cors)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["data"] = new Regex(@"(?:database|postgres|sql\s*server|entity\s*framework|repository|orm|migration)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["api"] = new Regex(@"(?:rest|graphql|grpc|web\s*api|minimal\s*api|swagger|openapi)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["messaging"] = new Regex(@"(?:message|queue|kafka|rabbitmq|service\s*bus|event|pub\s*/\s*sub)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["cloud"] = new Regex(@"(?:azure|aws|gcp|cloud|container|docker|kubernetes)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["frontend"] = new Regex(@"(?:react|angular|vue|blazor|spa|pwa|typescript|javascript)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["devops"] = new Regex(@"(?:ci/cd|pipeline|github\s*actions|azure\s*devops|terraform|deployment)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Framework reference patterns
    private static readonly Dictionary<string, Regex> FrameworkPatterns = new()
    {
        [".NET"] = new Regex(@"\.?net\s*(?:\d+(?:\.\d+)?)?|aspnet|asp\.net|c#|csharp", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["Entity Framework"] = new Regex(@"entity\s*framework|ef\s*core|ef\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["Azure"] = new Regex(@"azure|app\s*service|azure\s*functions|cosmos\s*db|azure\s*ad|azure\s*b2c", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["React"] = new Regex(@"react|next\.?js|gatsby", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["Angular"] = new Regex(@"angular", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["PostgreSQL"] = new Regex(@"postgres|postgresql|pgvector", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["Docker"] = new Regex(@"docker|container|dockerfile", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["Kubernetes"] = new Regex(@"kubernetes|k8s|kubectl|helm", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public MetadataEnricherService(
        ILogger<MetadataEnricherService> logger,
        MetadataEnrichmentConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new MetadataEnrichmentConfig();
    }

    /// <summary>
    /// Enrich a chunk with metadata
    /// </summary>
    public EnrichedMetadata EnrichChunk(DocumentChunk chunk, ParsedMarkdown? parsedMarkdown = null)
    {
        var metadata = new EnrichedMetadata
        {
            ChunkId = chunk.ChunkId,
            SourceDocumentId = chunk.SourceDocumentId,
            DocumentType = chunk.DocumentType
        };

        var content = chunk.Content;
        var contentLower = content.ToLowerInvariant();

        // Extract ADR status
        if (_config.ExtractAdrStatus && chunk.DocumentType == "ADR")
        {
            metadata.Status = ExtractAdrStatus(content);
        }

        // Analyze sentiment
        if (_config.AnalyzeSentiment)
        {
            metadata.Sentiment = AnalyzeSentiment(content);
        }

        // Classify category
        if (_config.ClassifyCategory)
        {
            metadata.Category = ClassifyCategory(contentLower);
        }

        // Extract technical tags
        if (_config.ExtractTechnicalTags)
        {
            metadata.TechnicalTags = ExtractTechnicalTags(contentLower);
        }

        // Detect framework references
        if (_config.DetectFrameworkRefs)
        {
            metadata.FrameworkRefs = DetectFrameworkReferences(content);
        }

        // Calculate quality score
        if (_config.CalculateQualityScore)
        {
            metadata.QualityScore = CalculateQualityScore(chunk, parsedMarkdown);
        }

        // Extract completeness indicators
        metadata.Completeness = AnalyzeCompleteness(content, chunk.DocumentType);

        _logger.LogDebug(
            "Enriched chunk {ChunkId}: Status={Status}, Sentiment={Sentiment}, Category={Category}, " +
            "Quality={Quality:F2}, Tags={TagCount}, Frameworks={FrameworkCount}",
            chunk.ChunkId,
            metadata.Status,
            metadata.Sentiment,
            metadata.Category,
            metadata.QualityScore,
            metadata.TechnicalTags.Count,
            metadata.FrameworkRefs.Count
        );

        return metadata;
    }

    /// <summary>
    /// Extract ADR status from content
    /// </summary>
    private string ExtractAdrStatus(string content)
    {
        var match = AdrStatusRegex.Match(content);
        if (match.Success)
        {
            // Try each capture group
            for (int i = 1; i <= match.Groups.Count; i++)
            {
                if (match.Groups[i].Success && !string.IsNullOrWhiteSpace(match.Groups[i].Value))
                {
                    return match.Groups[i].Value.ToLowerInvariant();
                }
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Analyze sentiment (positive, negative, neutral, warning)
    /// </summary>
    private string AnalyzeSentiment(string content)
    {
        var hasAntiPattern = AntiPatternRegex.IsMatch(content);
        var hasBestPractice = BestPracticeRegex.IsMatch(content);
        var hasWarning = WarningRegex.IsMatch(content);

        // Priority: warning > anti-pattern > best practice > neutral
        if (hasWarning)
            return "warning";
        if (hasAntiPattern)
            return "negative";
        if (hasBestPractice)
            return "positive";

        return "neutral";
    }

    /// <summary>
    /// Classify content into a category
    /// </summary>
    private string ClassifyCategory(string contentLower)
    {
        var categoryScores = new Dictionary<string, int>();

        foreach (var (category, pattern) in CategoryPatterns)
        {
            var matches = pattern.Matches(contentLower);
            if (matches.Count > 0)
            {
                categoryScores[category] = matches.Count;
            }
        }

        if (categoryScores.Count == 0)
            return "general";

        // Return category with highest match count
        return categoryScores.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>
    /// Extract technical tags from content
    /// </summary>
    private List<string> ExtractTechnicalTags(string contentLower)
    {
        var tags = new HashSet<string>();

        foreach (var (tag, pattern) in TechnicalTagPatterns)
        {
            if (pattern.IsMatch(contentLower))
            {
                tags.Add(tag);
            }
        }

        return tags.OrderBy(t => t).ToList();
    }

    /// <summary>
    /// Detect framework references
    /// </summary>
    private List<string> DetectFrameworkReferences(string content)
    {
        var frameworks = new HashSet<string>();

        foreach (var (framework, pattern) in FrameworkPatterns)
        {
            if (pattern.IsMatch(content))
            {
                frameworks.Add(framework);
            }
        }

        return frameworks.OrderBy(f => f).ToList();
    }

    /// <summary>
    /// Calculate quality score based on multiple factors
    /// </summary>
    private decimal CalculateQualityScore(DocumentChunk chunk, ParsedMarkdown? parsedMarkdown)
    {
        decimal score = 0m;

        // Factor 1: Length (30%)
        var lengthScore = CalculateLengthScore(chunk.Content.Length);
        score += lengthScore * _config.Weights.Length;

        // Factor 2: Structure (25%)
        var structureScore = CalculateStructureScore(chunk.Content, parsedMarkdown);
        score += structureScore * _config.Weights.Structure;

        // Factor 3: Examples (20%)
        var examplesScore = CalculateExamplesScore(chunk.Content);
        score += examplesScore * _config.Weights.Examples;

        // Factor 4: Completeness (15%)
        var completeness = AnalyzeCompleteness(chunk.Content, chunk.DocumentType);
        var completenessScore = CalculateCompletenessScore(completeness);
        score += completenessScore * _config.Weights.Completeness;

        // Factor 5: Clarity (10%)
        var clarityScore = CalculateClarityScore(chunk.Content);
        score += clarityScore * _config.Weights.Clarity;

        return Math.Max(0m, Math.Min(1m, score)); // Clamp to [0, 1]
    }

    /// <summary>
    /// Calculate score based on content length
    /// </summary>
    private decimal CalculateLengthScore(int length)
    {
        // Optimal range: 400-2400 characters (100-600 tokens)
        if (length < 200) return 0.3m;  // Too short
        if (length < 400) return 0.6m;  // Short
        if (length <= 2400) return 1.0m; // Optimal
        if (length <= 3600) return 0.8m; // Long
        return 0.5m; // Too long
    }

    /// <summary>
    /// Calculate score based on structure (headings, lists, paragraphs)
    /// </summary>
    private decimal CalculateStructureScore(string content, ParsedMarkdown? parsedMarkdown)
    {
        decimal score = 0m;

        // Check for headings
        if (content.Contains("##") || content.Contains("**"))
            score += 0.3m;

        // Check for lists
        if (content.Contains("\n- ") || content.Contains("\n* ") || content.Contains("\n1. "))
            score += 0.3m;

        // Check for multiple paragraphs
        var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (paragraphs.Length >= 2)
            score += 0.2m;

        // Check for code blocks
        if (content.Contains("```"))
            score += 0.2m;

        return Math.Min(1.0m, score);
    }

    /// <summary>
    /// Calculate score based on presence of examples
    /// </summary>
    private decimal CalculateExamplesScore(string content)
    {
        decimal score = 0m;

        // Code examples
        if (content.Contains("```"))
            score += 0.5m;

        // Inline code
        if (content.Contains("`"))
            score += 0.2m;

        // Example keywords
        if (Regex.IsMatch(content, @"(?:example|for instance|such as|e\.g\.|usage)", RegexOptions.IgnoreCase))
            score += 0.3m;

        return Math.Min(1.0m, score);
    }

    /// <summary>
    /// Calculate completeness score
    /// </summary>
    private decimal CalculateCompletenessScore(CompletenessMetadata completeness)
    {
        int totalChecks = 5;
        int passedChecks = 0;

        if (completeness.HasContext) passedChecks++;
        if (completeness.HasExamples) passedChecks++;
        if (completeness.HasConsequences) passedChecks++;
        if (completeness.LengthAdequate) passedChecks++;
        if (completeness.HasStructure) passedChecks++;

        return (decimal)passedChecks / totalChecks;
    }

    /// <summary>
    /// Calculate clarity score
    /// </summary>
    private decimal CalculateClarityScore(string content)
    {
        decimal score = 1.0m;

        // Penalize very long sentences
        var sentences = content.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries);
        var avgSentenceLength = sentences.Length > 0 ? content.Length / sentences.Length : 0;
        if (avgSentenceLength > 200)
            score -= 0.3m;

        // Reward clear structure words
        if (Regex.IsMatch(content, @"(?:first|second|finally|therefore|because|however)", RegexOptions.IgnoreCase))
            score += 0.2m;

        // Penalize excessive jargon without explanation
        var jargonCount = Regex.Matches(content, @"\b[A-Z]{3,}\b").Count;
        if (jargonCount > 5)
            score -= 0.2m;

        return Math.Max(0m, Math.Min(1m, score));
    }

    /// <summary>
    /// Analyze completeness of the content
    /// </summary>
    private CompletenessMetadata AnalyzeCompleteness(string content, string documentType)
    {
        var contentLower = content.ToLowerInvariant();

        var completeness = new CompletenessMetadata
        {
            HasContext = contentLower.Contains("context") || contentLower.Contains("background") || contentLower.Contains("problem"),
            HasExamples = content.Contains("```") || contentLower.Contains("example") || contentLower.Contains("usage"),
            HasConsequences = contentLower.Contains("consequence") || contentLower.Contains("impact") || contentLower.Contains("trade-off"),
            LengthAdequate = content.Length >= 200,
            HasStructure = content.Contains("##") || content.Contains("\n- ") || content.Contains("\n1. ")
        };

        return completeness;
    }

    /// <summary>
    /// Enrich multiple chunks in batch
    /// </summary>
    public List<EnrichedMetadata> EnrichChunksBatch(
        List<DocumentChunk> chunks,
        Dictionary<string, ParsedMarkdown>? parsedMarkdowns = null)
    {
        _logger.LogInformation("Enriching {Count} chunks with metadata...", chunks.Count);

        var enrichedList = new List<EnrichedMetadata>();
        var startTime = DateTime.UtcNow;

        foreach (var chunk in chunks)
        {
            ParsedMarkdown? parsed = null;
            if (parsedMarkdowns != null && parsedMarkdowns.TryGetValue(chunk.SourceDocumentId, out var p))
            {
                parsed = p;
            }

            var enriched = EnrichChunk(chunk, parsed);
            enrichedList.Add(enriched);
        }

        var elapsed = DateTime.UtcNow - startTime;

        _logger.LogInformation(
            "Enriched {Count} chunks in {Elapsed}ms. Avg: {AvgQuality:F2} quality, {AvgTags} tags per chunk",
            chunks.Count,
            elapsed.TotalMilliseconds,
            enrichedList.Average(e => (double)e.QualityScore),
            enrichedList.Average(e => e.TechnicalTags.Count)
        );

        return enrichedList;
    }
}
