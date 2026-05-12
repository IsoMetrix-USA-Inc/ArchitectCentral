# Metadata Enrichment Service

## Overview

The Metadata Enrichment Service automatically extracts and enriches document chunks with semantic metadata to improve search relevance, categorization, and query filtering in RAG (Retrieval-Augmented Generation) systems.

## Features

### 🔍 **Automatic Metadata Extraction**

1. **ADR Status Detection** - Extracts decision status from Architecture Decision Records
2. **Sentiment Analysis** - Identifies best practices, anti-patterns, and warnings
3. **Category Classification** - Assigns topic categories (authentication, data, API, etc.)
4. **Quality Scoring** - Calculates 0.0-1.0 score based on multiple quality factors
5. **Technical Tags** - Extracts relevant technology tags
6. **Framework Detection** - Identifies frameworks and technologies mentioned

### 📊 **Quality Score Components**

The quality score (0.0-1.0) is calculated using weighted factors:

| Factor | Weight | Description |
|--------|--------|-------------|
| Length | 30% | Content length in optimal range (400-2400 chars) |
| Structure | 25% | Headings, lists, paragraphs, code blocks |
| Examples | 20% | Presence of code examples and usage patterns |
| Completeness | 15% | Context, consequences, adequate depth |
| Clarity | 10% | Sentence structure, clear language |

## Configuration

### Basic Configuration

```csharp
var config = new MetadataEnrichmentConfig
{
    ExtractAdrStatus = true,
    AnalyzeSentiment = true,
    CalculateQualityScore = true,
    ExtractTechnicalTags = true,
    DetectFrameworkRefs = true,
    ClassifyCategory = true,
    MinimumQualityScore = 0.0m
};
```

### Custom Quality Weights

```csharp
var config = new MetadataEnrichmentConfig
{
    Weights = new QualityScoreWeights
    {
        Length = 0.25m,        // Adjust if length is less important
        Structure = 0.30m,     // Increase if structure is critical
        Examples = 0.25m,      // Increase for code-heavy docs
        Completeness = 0.15m,
        Clarity = 0.05m
    }
};
```

## Usage

### Enrich Single Chunk

```csharp
var enricher = new MetadataEnricherService(logger, config);
var chunk = new DocumentChunk { /* ... */ };
var parsedMarkdown = await parser.ParseAsync(content);

var metadata = enricher.EnrichChunk(chunk, parsedMarkdown);

Console.WriteLine($"Status: {metadata.Status}");
Console.WriteLine($"Sentiment: {metadata.Sentiment}");
Console.WriteLine($"Category: {metadata.Category}");
Console.WriteLine($"Quality: {metadata.QualityScore:F2}");
Console.WriteLine($"Tags: {string.Join(", ", metadata.TechnicalTags)}");
Console.WriteLine($"Frameworks: {string.Join(", ", metadata.FrameworkRefs)}");
```

### Batch Enrichment

```csharp
var chunks = new List<DocumentChunk> { /* ... */ };
var parsedMarkdowns = new Dictionary<string, ParsedMarkdown>();

var enrichedList = enricher.EnrichChunksBatch(chunks, parsedMarkdowns);

// Average quality across all chunks
var avgQuality = enrichedList.Average(e => (double)e.QualityScore);
Console.WriteLine($"Average quality score: {avgQuality:F2}");
```

## Metadata Extraction Details

### 1. ADR Status Extraction

**Supported Statuses:**
- `proposed` - ADR is under consideration
- `accepted` - ADR has been approved
- `deprecated` - ADR is no longer recommended
- `superseded` - ADR has been replaced by a newer one
- `unknown` - Status could not be determined

**Detection Patterns:**
```
## Status
**Accepted**

Status: Proposed

**Superseded** by ADR-0042
```

### 2. Sentiment Analysis

**Sentiment Types:**
- `positive` - Best practices, recommended approaches
- `negative` - Anti-patterns, things to avoid
- `warning` - Security concerns, important notes
- `neutral` - Informational content

**Detection Keywords:**

**Positive:**
- best practice, recommended, should, correct, right way, prefer, good practice, always, use this

**Negative:**
- anti-pattern, don't, avoid, not recommended, bad practice, incorrect, wrong way, never, do not use

**Warning:**
- warning, caution, note, important, beware, be careful, security, vulnerability, risk

**Priority:** Warning > Negative > Positive > Neutral

### 3. Category Classification

**Supported Categories:**

| Category | Keywords |
|----------|----------|
| authentication | auth, login, SSO, OAuth, OIDC, JWT, SAML, identity, Azure AD/B2C |
| data-architecture | database, PostgreSQL, SQL, Entity Framework, repository, ORM, migration |
| api-design | API, REST, GraphQL, endpoint, HTTP, web service, controller, Swagger |
| deployment | deploy, Docker, Kubernetes, container, Azure, AWS, cloud, CI/CD |
| frontend | UI, React, Angular, Vue, Blazor, TypeScript, JavaScript, SPA |
| architecture-pattern | microservice, monolith, CQRS, event sourcing, clean architecture, DDD |
| testing | test, unit test, integration test, XUnit, NUnit, mock, TDD, BDD |
| security | security, encryption, SSL, TLS, vulnerability, threat, CORS, CSRF |
| observability | monitoring, logging, telemetry, metrics, Application Insights, Serilog |
| performance | performance, optimization, cache, Redis, CDN, latency, scalability |
| general | Default for unmatched content |

**Algorithm:** Counts keyword matches per category, returns highest scoring category.

### 4. Technical Tags

**Tag Categories:**

| Tag | Keywords |
|-----|----------|
| architecture-patterns | CQRS, event sourcing, saga, clean architecture, vertical slice, DDD, microservices |
| security | authentication, authorization, encryption, SSL, OAuth, JWT, certificate |
| data | database, PostgreSQL, SQL Server, Entity Framework, repository, ORM |
| api | REST, GraphQL, gRPC, Web API, Minimal API, Swagger, OpenAPI |
| messaging | message, queue, Kafka, RabbitMQ, Service Bus, event, pub/sub |
| cloud | Azure, AWS, GCP, cloud, container, Docker, Kubernetes |
| frontend | React, Angular, Vue, Blazor, SPA, PWA, TypeScript |
| devops | CI/CD, pipeline, GitHub Actions, Azure DevOps, Terraform |

**Output:** Sorted list of matching tags (multiple tags possible)

### 5. Framework Detection

**Detected Frameworks:**

| Framework | Detection Pattern |
|-----------|-------------------|
| .NET | .NET, ASP.NET, C#, CSharp |
| Entity Framework | Entity Framework, EF Core, EF 6 |
| Azure | Azure, App Service, Azure Functions, Cosmos DB, Azure AD |
| React | React, Next.js, Gatsby |
| Angular | Angular |
| PostgreSQL | Postgres, PostgreSQL, pgvector |
| Docker | Docker, container, Dockerfile |
| Kubernetes | Kubernetes, k8s, kubectl, Helm |

**Output:** Sorted list of detected frameworks

### 6. Quality Score Calculation

#### Length Score (30%)

| Character Range | Score | Description |
|----------------|-------|-------------|
| < 200 | 0.3 | Too short, lacks detail |
| 200-400 | 0.6 | Short but acceptable |
| 400-2400 | 1.0 | Optimal range |
| 2400-3600 | 0.8 | Long but manageable |
| > 3600 | 0.5 | Too long, should be split |

#### Structure Score (25%)

- **+0.3** - Has headings (## or **)
- **+0.3** - Has lists (-, *, 1.)
- **+0.2** - Multiple paragraphs (2+)
- **+0.2** - Has code blocks (```)
- **Max:** 1.0

#### Examples Score (20%)

- **+0.5** - Has code blocks (```)
- **+0.2** - Has inline code (`)
- **+0.3** - Example keywords (example, for instance, e.g., usage)
- **Max:** 1.0

#### Completeness Score (15%)

Checks for:
- **HasContext** - Mentions context, background, problem
- **HasExamples** - Contains examples or code
- **HasConsequences** - Discusses consequences, impact, trade-offs
- **LengthAdequate** - At least 200 characters
- **HasStructure** - Has headings, lists, or formatting

**Formula:** (passed_checks / 5) * 1.0

#### Clarity Score (10%)

- **Base:** 1.0
- **-0.3** - Very long avg sentence length (>200 chars)
- **+0.2** - Clear structure words (first, second, therefore, because)
- **-0.2** - Excessive jargon (>5 all-caps acronyms)
- **Range:** 0.0 - 1.0

## Output Examples

### Example 1: High-Quality ADR Chunk

```csharp
var metadata = enricher.EnrichChunk(adrChunk);

// Output:
Status: "accepted"
Category: "authentication"
Sentiment: "positive"
QualityScore: 0.92
TechnicalTags: ["security", "api", "architecture-patterns"]
FrameworkRefs: ["Azure", ".NET", "Entity Framework"]
Completeness: {
    HasContext: true,
    HasExamples: true,
    HasConsequences: true,
    LengthAdequate: true,
    HasStructure: true
}
```

### Example 2: Warning Anti-Pattern

```csharp
// Content: "⚠️ **Warning**: Never store passwords in plain text..."

var metadata = enricher.EnrichChunk(warningChunk);

// Output:
Sentiment: "warning"  // Warning takes priority
Category: "security"
QualityScore: 0.75
TechnicalTags: ["security"]
```

### Example 3: Low-Quality Fragment

```csharp
// Content: "See above. TODO: add more details."

var metadata = enricher.EnrichChunk(fragmentChunk);

// Output:
Sentiment: "neutral"
Category: "general"
QualityScore: 0.35  // Low due to short length, no structure
TechnicalTags: []
FrameworkRefs: []
Completeness: {
    HasContext: false,
    HasExamples: false,
    HasConsequences: false,
    LengthAdequate: false,  // < 200 chars
    HasStructure: false
}
```

## Integration with Enhanced Chunking

The metadata enricher integrates seamlessly with the Enhanced Chunking Service:

```csharp
// 1. Chunk documents
var enhancedChunker = new EnhancedChunkingService(chunkingConfig, logger);
var chunks = await enhancedChunker.ChunkWithOverlapAsync(/* ... */);

// 2. Enrich with metadata
var enricher = new MetadataEnricherService(enricherLogger, enrichmentConfig);
var enrichedMetadata = enricher.EnrichChunksBatch(chunks);

// 3. Apply metadata to chunks
for (int i = 0; i < chunks.Count; i++)
{
    var chunk = chunks[i] as EnhancedDocumentChunk;
    var metadata = enrichedMetadata[i];
    
    chunk.Status = metadata.Status;
    chunk.Category = metadata.Category;
    chunk.Sentiment = metadata.Sentiment;
    chunk.QualityScore = metadata.QualityScore;
    chunk.TechnicalTags = metadata.TechnicalTags;
    chunk.FrameworkRefs = metadata.FrameworkRefs;
}
```

## Performance Considerations

### Batch Processing

Enrich chunks in batches for better performance:

```csharp
// Good: Batch processing
var enrichedList = enricher.EnrichChunksBatch(allChunks);

// Less efficient: Individual enrichment
foreach (var chunk in allChunks)
{
    var metadata = enricher.EnrichChunk(chunk);  // Don't do this in a loop
}
```

### Regex Performance

All regex patterns are:
- **Pre-compiled** - Compiled once at service initialization
- **Static** - Shared across all instances
- **Optimized** - Use appropriate regex options for performance

### Typical Performance

- **Single chunk:** ~1-2ms
- **Batch (100 chunks):** ~150-200ms
- **Average:** ~1.5ms per chunk

## Best Practices

### 1. Configure for Your Domain

```csharp
// For code-heavy documentation
config.Weights.Examples = 0.30m;
config.Weights.Structure = 0.30m;
config.Weights.Length = 0.20m;

// For architecture decision records
config.Weights.Completeness = 0.25m;
config.Weights.Structure = 0.30m;
config.Weights.Clarity = 0.15m;
```

### 2. Filter by Quality

```csharp
// Only index high-quality chunks
var highQualityChunks = enrichedList
    .Where(m => m.QualityScore >= 0.70m)
    .ToList();
```

### 3. Use Metadata for Search Filters

```sql
-- Query V2 schema with metadata filters
SELECT * FROM document_chunks_v2
WHERE status = 'accepted'
  AND category = 'authentication'
  AND quality_score >= 0.75
  AND 'security' = ANY(technical_tags)
ORDER BY quality_score DESC;
```

### 4. Monitor Quality Trends

```csharp
// Log quality statistics
logger.LogInformation(
    "Enrichment complete. Avg Quality: {AvgQuality:F2}, " +
    "High Quality (>0.7): {HighQualityCount} ({HighQualityPercent:F1}%)",
    enrichedList.Average(e => (double)e.QualityScore),
    enrichedList.Count(e => e.QualityScore >= 0.70m),
    enrichedList.Count(e => e.QualityScore >= 0.70m) * 100.0 / enrichedList.Count
);
```

## Troubleshooting

### Issue: Low Quality Scores Across All Content

**Symptoms:** Most chunks score <0.5

**Solutions:**
1. Adjust quality weights for your content type
2. Lower length requirements for shorter documents
3. Review structure expectations (headings, lists)

### Issue: Wrong Categories Assigned

**Symptoms:** Chunks miscategorized

**Solutions:**
1. Add domain-specific keywords to `CategoryPatterns`
2. Increase keyword specificity in regex patterns
3. Use custom category classification logic

### Issue: Missing Technical Tags

**Symptoms:** Expected tags not extracted

**Solutions:**
1. Add technology-specific patterns to `TechnicalTagPatterns`
2. Verify keyword variations are included
3. Check for case-sensitivity issues

## Examples by Document Type

### ADR Example

```csharp
// Input: ADR-0015-use-azure-ad-b2c.md
var metadata = enricher.EnrichChunk(adrChunk);

// Expected output:
Status: "accepted"
Category: "authentication"
Sentiment: "positive"
QualityScore: 0.88
TechnicalTags: ["security", "cloud", "api"]
FrameworkRefs: ["Azure", ".NET"]
```

### Guideline Example

```csharp
// Input: API-Guidelines.md
var metadata = enricher.EnrichChunk(guidelineChunk);

// Expected output:
Status: "unknown"  // Not an ADR
Category: "api-design"
Sentiment: "positive"  // Contains best practices
QualityScore: 0.82
TechnicalTags: ["api", "architecture-patterns"]
FrameworkRefs: [".NET", "Entity Framework"]
```

### Anti-Pattern Example

```csharp
// Input: common-mistakes.md
var metadata = enricher.EnrichChunk(antiPatternChunk);

// Expected output:
Status: "unknown"
Category: "security"  // If security-related
Sentiment: "negative"  // Anti-pattern detected
QualityScore: 0.71
TechnicalTags: ["security", "data"]
FrameworkRefs: []
```

## References

- [RAG Research Findings](../../../files/rag-research-findings.md)
- [Enhanced Schema Design](../../../files/enhanced-schema-design.md)
- [Enhanced Chunking Service](../Chunking/README.md)

---

**Version:** 1.0.0  
**Last Updated:** 2026-05-11  
**Status:** Production Ready
