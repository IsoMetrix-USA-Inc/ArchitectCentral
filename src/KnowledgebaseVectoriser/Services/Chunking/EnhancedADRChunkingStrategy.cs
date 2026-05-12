using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace KnowledgebaseVectoriser.Services.Chunking
{
    /// <summary>
    /// Enhanced ADR chunking strategy with V2 schema support
    /// Implements smart overlap at section boundaries and extracts ADR-specific metadata
    /// </summary>
    public class EnhancedADRChunkingStrategy : BaseChunkingStrategy
    {
        private readonly ILogger<EnhancedADRChunkingStrategy> _logger;
        private readonly EnhancedChunkingService _enhancedService;

        // ADR sections that should be kept as separate chunks when possible
        private readonly HashSet<string> _adrSections = new(StringComparer.OrdinalIgnoreCase)
        {
            "status", "context", "decision", "consequences",
            "alternatives", "implications", "related decisions",
            "positive", "negative", "risks", "neutral"
        };

        // Regex to extract ADR status from content
        private static readonly Regex StatusRegex = new(
            @"\*\*(Accepted|Proposed|Deprecated|Superseded)\*\*|Status:?\s*(Accepted|Proposed|Deprecated|Superseded)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Regex to extract ADR number from content
        private static readonly Regex AdrNumberRegex = new(
            @"#\s*(\d{4})\.|ADR[-\s]?(\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public EnhancedADRChunkingStrategy(
            ILogger<EnhancedADRChunkingStrategy> logger,
            EnhancedChunkingConfig config,
            EnhancedChunkingService enhancedService)
            : base(config)
        {
            _logger = logger;
            _enhancedService = enhancedService;
        }

        public override async Task<List<DocumentChunk>> ChunkAsync(
            DocumentInfo document,
            string content,
            ParsedMarkdown parsedMarkdown)
        {
            _logger.LogDebug("Enhanced ADR chunking: {Path}", document.RelativePath);

            // Extract ADR ID and metadata
            var fileName = Path.GetFileNameWithoutExtension(document.FilePath);
            var adrNumber = ExtractAdrNumber(fileName, content);
            var adrId = $"adr_{adrNumber:D4}";
            var adrStatus = ExtractAdrStatus(content);

            _logger.LogInformation("Processing ADR-{Number} with status: {Status}", adrNumber, adrStatus);

            // Determine category from title/sections
            var category = DetermineAdrCategory(parsedMarkdown);

            // Group sections with semantic overlap
            var sectionGroups = GroupADRSectionsWithOverlap(parsedMarkdown.Sections);

            var chunks = new List<DocumentChunk>();
            var chunkIndex = 0;

            foreach (var group in sectionGroups)
            {
                // Build group content with proper formatting
                var groupContent = BuildGroupContent(group);
                var tokenCount = EstimateTokenCount(groupContent);

                if (tokenCount <= _config.MaxChunkSize)
                {
                    // Group fits in one chunk
                    var chunk = CreateEnhancedChunk(
                        document,
                        adrId,
                        chunkIndex++,
                        groupContent,
                        group.Sections.First(),
                        group,
                        adrStatus,
                        category
                    );
                    chunks.Add(chunk);
                }
                else
                {
                    // Need to split the group with overlap
                    _logger.LogDebug(
                        "Section group '{GroupName}' is too large ({Tokens} tokens), splitting with {Overlap}% overlap",
                        group.GroupName,
                        tokenCount,
                        ((EnhancedChunkingConfig)_config).OverlapPercentage * 100
                    );

                    var splitChunks = SplitWithSemanticOverlap(groupContent, group);

                    foreach (var splitContent in splitChunks)
                    {
                        var chunk = CreateEnhancedChunk(
                            document,
                            adrId,
                            chunkIndex++,
                            splitContent,
                            group.Sections.First(),
                            group,
                            adrStatus,
                            category
                        );
                        chunks.Add(chunk);
                    }
                }
            }

            // Apply minimum chunk size enforcement
            if (_config.MergeShortChunks && chunks.Count > 1)
            {
                var originalCount = chunks.Count;
                chunks = MergeShortDocumentChunks(chunks, _config.MinChunkSize);

                if (chunks.Count < originalCount)
                {
                    _logger.LogDebug("Merged short chunks: {Original} -> {Final}", originalCount, chunks.Count);

                    // Renumber chunk IDs after merging
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        chunks[i].ChunkId = CreateChunkId(adrId, i);
                    }
                }
            }

            _logger.LogInformation(
                "Chunked ADR-{Number} into {Count} chunks. Avg tokens: {AvgTokens}",
                adrNumber,
                chunks.Count,
                chunks.Average(c => EstimateTokenCount(c.Content))
            );

            return await Task.FromResult(chunks);
        }

        /// <summary>
        /// Extract ADR number from filename or content
        /// </summary>
        private int ExtractAdrNumber(string fileName, string content)
        {
            // Try filename first
            if (fileName.Length >= 4 && int.TryParse(fileName.Substring(0, 4), out int fileNumber))
            {
                return fileNumber;
            }

            // Try content
            var match = AdrNumberRegex.Match(content);
            if (match.Success)
            {
                var numberStr = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (int.TryParse(numberStr, out int contentNumber))
                {
                    return contentNumber;
                }
            }

            // Default to 0000
            _logger.LogWarning("Could not extract ADR number from {FileName}, using 0000", fileName);
            return 0;
        }

        /// <summary>
        /// Extract ADR status from content
        /// </summary>
        private string ExtractAdrStatus(string content)
        {
            var match = StatusRegex.Match(content);
            if (match.Success)
            {
                var status = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                return status.ToLowerInvariant();
            }

            return "unknown";
        }

        /// <summary>
        /// Determine ADR category from title and sections
        /// </summary>
        private string DetermineAdrCategory(ParsedMarkdown markdown)
        {
            var allText = string.Join(" ", markdown.Sections.Select(s => s.Title + " " + s.Content))
                .ToLowerInvariant();

            // Common ADR categories
            if (allText.Contains("authentication") || allText.Contains("authorization"))
                return "authentication";
            if (allText.Contains("database") || allText.Contains("data"))
                return "data-architecture";
            if (allText.Contains("api") || allText.Contains("rest") || allText.Contains("graphql"))
                return "api-design";
            if (allText.Contains("deployment") || allText.Contains("infrastructure"))
                return "deployment";
            if (allText.Contains("frontend") || allText.Contains("ui"))
                return "frontend";
            if (allText.Contains("microservice") || allText.Contains("monolith"))
                return "architecture-pattern";
            if (allText.Contains("testing") || allText.Contains("quality"))
                return "testing";
            if (allText.Contains("security"))
                return "security";
            if (allText.Contains("monitoring") || allText.Contains("observability"))
                return "observability";

            return "general";
        }

        /// <summary>
        /// Group ADR sections with semantic overlap considerations
        /// </summary>
        private List<EnhancedSectionGroup> GroupADRSectionsWithOverlap(List<MarkdownSection> sections)
        {
            var groups = new List<EnhancedSectionGroup>();

            foreach (var section in sections)
            {
                var sectionTitle = section.Title.ToLowerInvariant().Trim();

                // Check if this is a standard ADR section
                bool isStandardSection = _adrSections.Any(adrSec => sectionTitle.Contains(adrSec));

                if (isStandardSection)
                {
                    // Standard ADR sections get their own group
                    groups.Add(new EnhancedSectionGroup
                    {
                        GroupName = section.Title,
                        Sections = new List<MarkdownSection> { section },
                        IsStandardSection = true,
                        SectionType = DetermineSectionType(section.Title),
                        PreferredOverlapStrategy = "semantic"
                    });
                }
                else
                {
                    // Other sections can be grouped together
                    if (groups.Count > 0 && !groups.Last().IsStandardSection)
                    {
                        groups.Last().Sections.Add(section);
                    }
                    else
                    {
                        groups.Add(new EnhancedSectionGroup
                        {
                            GroupName = "Other Sections",
                            Sections = new List<MarkdownSection> { section },
                            IsStandardSection = false,
                            SectionType = "other",
                            PreferredOverlapStrategy = "paragraph"
                        });
                    }
                }
            }

            return groups;
        }

        /// <summary>
        /// Determine the semantic type of a section
        /// </summary>
        private string DetermineSectionType(string title)
        {
            var titleLower = title.ToLowerInvariant();

            if (titleLower.Contains("status")) return "status";
            if (titleLower.Contains("context")) return "context";
            if (titleLower.Contains("decision")) return "decision";
            if (titleLower.Contains("consequence")) return "consequences";
            if (titleLower.Contains("alternative")) return "alternatives";
            if (titleLower.Contains("positive")) return "consequences-positive";
            if (titleLower.Contains("negative")) return "consequences-negative";
            if (titleLower.Contains("risk")) return "consequences-risks";
            if (titleLower.Contains("related")) return "related-decisions";

            return "other";
        }

        /// <summary>
        /// Build group content with proper formatting and context
        /// </summary>
        private string BuildGroupContent(EnhancedSectionGroup group)
        {
            var content = new StringBuilder();

            // Add section context header for overlap
            if (group.Sections.Count > 0)
            {
                content.AppendLine($"## {group.GroupName}");
                content.AppendLine();
            }

            foreach (var section in group.Sections)
            {
                if (group.Sections.Count > 1)
                {
                    content.AppendLine($"### {section.Title}");
                    content.AppendLine();
                }

                content.AppendLine(section.Content.Trim());
                content.AppendLine();
            }

            return content.ToString().Trim();
        }

        /// <summary>
        /// Split content with semantic overlap at section boundaries
        /// </summary>
        private List<string> SplitWithSemanticOverlap(string content, EnhancedSectionGroup group)
        {
            var chunks = new List<string>();
            var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            var currentChunk = new StringBuilder();
            var currentTokens = 0;
            var overlapBuffer = new Queue<string>();
            var overlapTokens = 0;
            var maxOverlapTokens = ((EnhancedChunkingConfig)_config).CalculateOverlapSize();

            foreach (var paragraph in paragraphs)
            {
                var paragraphTokens = EstimateTokenCount(paragraph);
                var testTokens = currentTokens + paragraphTokens;

                if (testTokens > _config.MaxChunkSize && currentTokens > 0)
                {
                    // Save current chunk
                    chunks.Add(currentChunk.ToString().Trim());

                    // Start new chunk with overlap buffer
                    currentChunk.Clear();
                    currentTokens = 0;

                    // Add overlap from buffer
                    while (overlapBuffer.Count > 0)
                    {
                        var overlapPara = overlapBuffer.Dequeue();
                        currentChunk.AppendLine(overlapPara);
                        currentChunk.AppendLine();
                        currentTokens += EstimateTokenCount(overlapPara);
                    }

                    overlapTokens = 0;
                }

                // Add paragraph to current chunk
                currentChunk.AppendLine(paragraph);
                currentChunk.AppendLine();
                currentTokens += paragraphTokens;

                // Update overlap buffer
                overlapBuffer.Enqueue(paragraph);
                overlapTokens += paragraphTokens;

                // Maintain overlap buffer size
                while (overlapTokens > maxOverlapTokens && overlapBuffer.Count > 1)
                {
                    var removed = overlapBuffer.Dequeue();
                    overlapTokens -= EstimateTokenCount(removed);
                }
            }

            // Add final chunk
            if (currentTokens > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <summary>
        /// Create enhanced chunk with V2 schema metadata
        /// </summary>
        private DocumentChunk CreateEnhancedChunk(
            DocumentInfo document,
            string documentId,
            int chunkIndex,
            string content,
            MarkdownSection primarySection,
            EnhancedSectionGroup group,
            string adrStatus,
            string category)
        {
            // Build enriched metadata
            var metadata = new Dictionary<string, object>
            {
                ["file_name"] = document.FileName,
                ["file_path"] = document.RelativePath,
                ["doc_type"] = "ADR",
                ["last_modified"] = document.LastModified.ToString("O"),
                ["section_title"] = primarySection.Title,
                ["section_level"] = primarySection.Level,
                ["section_type"] = group.SectionType,
                ["is_standard_section"] = group.IsStandardSection,
                ["adr_status"] = adrStatus,
                ["category"] = category,
                ["has_code_blocks"] = primarySection.CodeBlocks.Count > 0,
                ["code_block_count"] = primarySection.CodeBlocks.Count
            };

            return new DocumentChunk
            {
                ChunkId = CreateChunkId(documentId, chunkIndex),
                SourceDocumentId = documentId,
                DocumentType = "ADR",
                Section = primarySection.Title,
                Content = content,
                ChunkIndex = chunkIndex,
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(metadata),
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Enhanced section group with overlap metadata
        /// </summary>
        private class EnhancedSectionGroup
        {
            public string GroupName { get; set; } = string.Empty;
            public List<MarkdownSection> Sections { get; set; } = new();
            public bool IsStandardSection { get; set; }
            public string SectionType { get; set; } = string.Empty;
            public string PreferredOverlapStrategy { get; set; } = string.Empty;
        }
    }
}
