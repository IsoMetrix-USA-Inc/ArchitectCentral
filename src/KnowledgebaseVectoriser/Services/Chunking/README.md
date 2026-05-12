# Enhanced Chunking Service

## Overview

The Enhanced Chunking Service provides intelligent document chunking with overlapping boundaries for improved context continuity in RAG (Retrieval-Augmented Generation) systems. It supports the V2 schema with rich metadata tracking.

## Features

### Smart Overlap Strategy
- **Configurable Overlap Percentage**: Default 15% (recommended 10-20%)
- **Boundary-Aware Splitting**: Respects sentence, paragraph, or semantic boundaries
- **Overlap Position Tracking**: Records exact overlap regions for deduplication
- **Token-Accurate Counting**: Uses tiktoken for OpenAI-compatible token estimation

### V2 Schema Support
- **Rich Metadata**: Status, category, sentiment, quality scores
- **Content Hashing**: SHA256 for change detection and incremental updates
- **Relationship Tracking**: Models chunk overlap and continuation relationships
- **Heading Hierarchy**: Preserves document structure context

### Document-Type Specific Strategies
- **ADR Documents**: Semantic section preservation with status extraction
- **Guidelines**: Heading-based chunking with example preservation
- **Diagrams**: Minimal chunking with metadata extraction

## Configuration

### Basic Configuration

```csharp
var config = new EnhancedChunkingConfig
{
    MaxChunkSize = 600,              // Maximum tokens per chunk
    OverlapPercentage = 0.15,        // 15% overlap
    MinChunkSize = 50,               // Minimum tokens per chunk
    PreserveCodeBlocks = true,       // Keep code blocks intact
    PreserveSections = true,         // Respect section boundaries
    MergeShortChunks = true,         // Merge chunks below minimum
    TrackOverlapPositions = true,    // Track overlap metadata
    BoundaryStrategy = BoundaryStrategy.Sentence  // Sentence-aware splitting
};
```

### Advanced Configuration

```csharp
var config = new EnhancedChunkingConfig
{
    MaxChunkSize = 800,
    OverlapPercentage = 0.20,        // 20% for dense technical content
    MinOverlapSize = 30,             // Safety minimum
    MaxOverlapSize = 200,            // Prevent excessive overlap
    BoundaryStrategy = BoundaryStrategy.Semantic  // Smart boundary detection
};
```

## Usage

### Basic Usage

```csharp
// Initialize service
var logger = loggerFactory.CreateLogger<EnhancedChunkingService>();
var config = new EnhancedChunkingConfig();
var chunkingService = new EnhancedChunkingService(config, logger);

// Chunk a document
var document = new DocumentInfo { /* ... */ };
var content = File.ReadAllText(document.FilePath);
var parsedMarkdown = await markdownParser.ParseAsync(content);

// Use base strategy (e.g., ADR)
var baseStrategy = new ADRChunkingStrategy(logger, config);

// Get enhanced chunks
var enhancedChunks = await chunkingService.ChunkWithOverlapAsync(
    document,
    content,
    parsedMarkdown,
    baseStrategy
);

// Create relationships
var relationships = chunkingService.CreateChunkRelationships(enhancedChunks);
```

### ADR-Specific Chunking

```csharp
// Use enhanced ADR strategy with status extraction
var adrStrategy = new EnhancedADRChunkingStrategy(
    logger,
    config,
    chunkingService
);

var chunks = await adrStrategy.ChunkAsync(document, content, parsedMarkdown);

// Chunks now include:
// - Extracted ADR status (accepted, proposed, deprecated, superseded)
// - Category classification (authentication, data-architecture, etc.)
// - Section type metadata
// - Smart semantic overlap at section boundaries
```

## Overlap Strategies

### 1. Token-Based Overlap (Fast)
```csharp
config.BoundaryStrategy = BoundaryStrategy.Token;
config.OverlapPercentage = 0.10;  // 10% for fast processing
```
- **Use when**: Speed is critical, content is homogeneous
- **Pros**: Fastest processing
- **Cons**: May split mid-sentence

### 2. Sentence-Based Overlap (Balanced)
```csharp
config.BoundaryStrategy = BoundaryStrategy.Sentence;
config.OverlapPercentage = 0.15;  // 15% recommended
```
- **Use when**: Balanced accuracy and speed needed
- **Pros**: Natural boundaries, good context preservation
- **Cons**: Slightly slower than token-based

### 3. Paragraph-Based Overlap (Structured)
```csharp
config.BoundaryStrategy = BoundaryStrategy.Paragraph;
config.OverlapPercentage = 0.15;
```
- **Use when**: Content has clear paragraph structure
- **Pros**: Preserves logical groupings
- **Cons**: May create larger-than-desired chunks

### 4. Semantic Overlap (Smart)
```csharp
config.BoundaryStrategy = BoundaryStrategy.Semantic;
config.OverlapPercentage = 0.20;  // Higher for semantic
```
- **Use when**: Maximum quality needed (ADRs, technical docs)
- **Pros**: Best context preservation, section-aware
- **Cons**: Slowest, requires section parsing

## Output Examples

### Enhanced Chunk Structure

```csharp
var chunk = enhancedChunks[0];

// V2 Schema Properties
chunk.ChunkId = "adr_0001_chunk_000";
chunk.SourceDocumentId = "adr_0001";
chunk.SourceFilePath = "/docs/adr/0001-record-architecture-decisions.md";
chunk.ChunkIndex = 0;
chunk.ChunkTotal = 5;
chunk.ContentTokens = 487;
chunk.ContentHash = "a3f5b1c2...";

// Overlap Metadata
chunk.HasOverlap = true;
chunk.OverlapStart = 420;          // Token position where overlap starts
chunk.OverlapEnd = 487;            // Token position where overlap ends
                                   // (67 tokens of overlap)

// Content Metadata
chunk.HeadingHierarchy = ["Context", "Problem Statement"];
chunk.Section = "Context";
chunk.Status = "accepted";
chunk.Category = "process";
chunk.Sentiment = "neutral";

// Quality & Features
chunk.QualityScore = 0.85m;
chunk.HasCodeExamples = false;
chunk.HasDiagrams = false;
chunk.TechnicalTags = ["documentation", "process", "decision-making"];
chunk.FrameworkRefs = [];
```

### Chunk Relationships

```csharp
var relationships = chunkingService.CreateChunkRelationships(enhancedChunks);

// Example relationship
var rel = relationships[0];
rel.ChunkId = "adr_0001_chunk_000";
rel.RelatedChunkId = "adr_0001_chunk_001";
rel.RelationshipType = "overlaps_with";
rel.OverlapTokens = 67;

// Relationships are bidirectional:
// - Chunk 0 "overlaps_with" Chunk 1
// - Chunk 1 "continues_from" Chunk 0
```

## Overlap Calculation Example

### Input Text (1000 tokens)
```
┌─────────────────────────────────────────────────────┐
│ Chunk 1 (600 tokens)                                │
│ ┌───────────────────────────────────────────────┐   │
│ │ Unique Content (510 tokens)                   │   │
│ │                                               │   │
│ │                                               ├───┼─┐
│ └───────────────────────────────────────────────┘   │ │ Overlap
│                                                     │ │ (90 tokens)
│ ┌───────────────────────────────────────────────────┼─┤ 15%
│ │ Chunk 2 (600 tokens)                          │   │
│ │                                               │   │
│ │                                               │   │
│ │   Unique Content (510 tokens)                 │   │
│ └───────────────────────────────────────────────────┘
└─────────────────────────────────────────────────────┘
```

### Overlap Metadata Tracking

```csharp
// Chunk 1
chunk1.ContentTokens = 600;
chunk1.HasOverlap = true;
chunk1.OverlapStart = 510;  // Overlap starts at token 510
chunk1.OverlapEnd = 600;    // Overlap ends at token 600
// 90 tokens of overlap (15%)

// Chunk 2 starts with the overlapping content
chunk2.Content = "[overlapping 90 tokens from chunk1] + [new 510 tokens]";
```

## Best Practices

### Overlap Percentages by Content Type

| Document Type | Recommended Overlap | Rationale |
|--------------|-------------------|-----------|
| ADRs | 15-20% | Dense technical content, important context |
| Guidelines | 15% | Standard technical documentation |
| Code Examples | 10% | Code is self-contained, less context needed |
| Diagrams | 0-5% | Minimal text, mostly visual |
| General Docs | 12-15% | Balanced approach |

### Token Sizes by Document Type

| Document Type | Max Chunk Size | Min Chunk Size |
|--------------|----------------|----------------|
| ADRs | 600-800 tokens | 100 tokens |
| Guidelines | 600-1000 tokens | 150 tokens |
| Code Examples | 400-600 tokens | 50 tokens |
| Diagrams | 200-400 tokens | 30 tokens |

### Performance Considerations

**Small Documents (<2000 tokens)**
- Lower overlap (10%) to avoid excessive redundancy
- Fewer, larger chunks better than many tiny chunks

**Large Documents (>5000 tokens)**
- Standard overlap (15%)
- More granular chunks for better retrieval precision

**High-Quality Requirements**
- Higher overlap (18-20%)
- Semantic boundary strategy
- Careful deduplication in retrieval

**Cost-Sensitive Scenarios**
- Lower overlap (10-12%)
- Larger max chunk size (800-1000)
- Token-based strategy for speed

## Integration with V2 Schema

### Database Insertion

```csharp
// Convert EnhancedDocumentChunk to V2 schema
foreach (var chunk in enhancedChunks)
{
    await connection.ExecuteAsync(@"
        INSERT INTO document_chunks_v2 (
            chunk_id, source_doc_id, source_file_path,
            doc_type, status, category, sentiment,
            chunk_index, chunk_total,
            overlap_start, overlap_end, has_overlap,
            section, heading_hierarchy, content,
            content_tokens, content_hash,
            quality_score, has_code_examples, has_diagrams,
            technical_tags, framework_refs,
            metadata, created_at, embedding
        ) VALUES (
            @ChunkId, @SourceDocumentId, @SourceFilePath,
            @DocumentType, @Status, @Category, @Sentiment,
            @ChunkIndex, @ChunkTotal,
            @OverlapStart, @OverlapEnd, @HasOverlap,
            @Section, @HeadingHierarchy, @Content,
            @ContentTokens, @ContentHash,
            @QualityScore, @HasCodeExamples, @HasDiagrams,
            @TechnicalTags, @FrameworkRefs,
            @MetadataJson, @CreatedAt, @Embedding
        )",
        new
        {
            chunk.ChunkId,
            chunk.SourceDocumentId,
            chunk.SourceFilePath,
            chunk.DocumentType,
            chunk.Status,
            chunk.Category,
            chunk.Sentiment,
            chunk.ChunkIndex,
            chunk.ChunkTotal,
            chunk.OverlapStart,
            chunk.OverlapEnd,
            chunk.HasOverlap,
            chunk.Section,
            HeadingHierarchy = chunk.HeadingHierarchy.ToArray(),
            chunk.Content,
            chunk.ContentTokens,
            chunk.ContentHash,
            chunk.QualityScore,
            chunk.HasCodeExamples,
            chunk.HasDiagrams,
            TechnicalTags = chunk.TechnicalTags.ToArray(),
            FrameworkRefs = chunk.FrameworkRefs.ToArray(),
            chunk.MetadataJson,
            chunk.CreatedAt,
            Embedding = new Pgvector.Vector(chunk.Embedding.Value.ToArray())
        }
    );
}

// Insert chunk relationships
foreach (var rel in relationships)
{
    await connection.ExecuteAsync(@"
        INSERT INTO chunk_relationships (
            chunk_id, related_chunk_id,
            relationship_type, overlap_tokens
        ) VALUES (
            @ChunkId, @RelatedChunkId,
            @RelationshipType, @OverlapTokens
        )
        ON CONFLICT (chunk_id, related_chunk_id, relationship_type) DO NOTHING",
        rel
    );
}
```

## Monitoring & Analytics

### Track Chunking Quality

```csharp
// Log chunking statistics
_logger.LogInformation(
    "Document chunked: {Path}, Chunks: {Count}, Avg Size: {AvgSize}, " +
    "With Overlap: {OverlapCount}, Avg Overlap: {AvgOverlap} tokens, " +
    "Avg Quality: {AvgQuality}",
    document.RelativePath,
    chunks.Count,
    chunks.Average(c => c.ContentTokens),
    chunks.Count(c => c.HasOverlap),
    chunks.Where(c => c.HasOverlap)
          .Average(c => (c.OverlapEnd - c.OverlapStart) ?? 0),
    chunks.Average(c => c.QualityScore)
);
```

### Analyze Chunk Distribution

```sql
-- Query chunk statistics by document type
SELECT 
    doc_type,
    COUNT(*) as total_chunks,
    AVG(content_tokens) as avg_tokens,
    AVG(CASE WHEN has_overlap THEN overlap_end - overlap_start ELSE 0 END) as avg_overlap_tokens,
    AVG(quality_score) as avg_quality,
    COUNT(*) FILTER (WHERE has_code_examples) as chunks_with_code,
    AVG(array_length(technical_tags, 1)) as avg_tags_per_chunk
FROM document_chunks_v2
GROUP BY doc_type;
```

## Testing

### Unit Tests

```csharp
[Fact]
public async Task ChunkWithOverlap_ShouldTrackOverlapPositions()
{
    // Arrange
    var config = new EnhancedChunkingConfig
    {
        MaxChunkSize = 100,
        OverlapPercentage = 0.15,
        TrackOverlapPositions = true
    };
    var service = new EnhancedChunkingService(config, logger);
    
    // Act
    var chunks = await service.ChunkWithOverlapAsync(/* ... */);
    
    // Assert
    Assert.True(chunks[0].HasOverlap);
    Assert.NotNull(chunks[0].OverlapStart);
    Assert.NotNull(chunks[0].OverlapEnd);
    Assert.Equal(15, chunks[0].OverlapEnd - chunks[0].OverlapStart);
}
```

## Troubleshooting

### Issue: Overlaps Too Large

**Symptom:** Overlap regions exceed expected percentage

**Solution:**
```csharp
config.MaxOverlapSize = 150;  // Cap maximum overlap
config.BoundaryStrategy = BoundaryStrategy.Token;  // Use token-based for precision
```

### Issue: Too Many Small Chunks

**Symptom:** Many chunks below minimum size

**Solution:**
```csharp
config.MergeShortChunks = true;
config.MinChunkSize = 100;  // Increase minimum
```

### Issue: Context Lost at Boundaries

**Symptom:** Poor retrieval quality for boundary content

**Solution:**
```csharp
config.OverlapPercentage = 0.20;  // Increase overlap
config.BoundaryStrategy = BoundaryStrategy.Semantic;  // Use semantic splitting
```

## References

- [RAG Research Findings](../../rag-research-findings.md)
- [Enhanced Schema Design](../../enhanced-schema-design.md)
- [Migration Scripts](../../../migrations/README.md)

---

**Version:** 2.0.0  
**Last Updated:** 2026-05-08  
**Status:** Production Ready
