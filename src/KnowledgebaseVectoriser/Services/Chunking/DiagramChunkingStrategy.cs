using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services.Chunking
{
    /// <summary>
    /// Chunking strategy for Diagrams
    /// Diagrams are typically treated as single units with rich metadata
    /// </summary>
    public class DiagramChunkingStrategy : BaseChunkingStrategy
    {
        private readonly ILogger<DiagramChunkingStrategy> _logger;

        public DiagramChunkingStrategy(ILogger<DiagramChunkingStrategy> logger, ChunkingConfig config)
            : base(config)
        {
            _logger = logger;
        }

        public override async Task<List<DocumentChunk>> ChunkAsync(DocumentInfo document, string content, ParsedMarkdown parsedMarkdown)
        {
            _logger.LogDebug("Chunking Diagram: {Path}", document.RelativePath);

            var diagramId = $"diagram_{Path.GetFileNameWithoutExtension(document.FilePath)}";

            // Extract diagram type from filename or content
            var diagramType = ExtractDiagramType(document.FileName, content);

            // Build searchable content: title + description + extracted text from diagram
            var searchableContent = BuildSearchableContent(parsedMarkdown);

            var metadata = new Dictionary<string, string>
            {
                ["diagram_type"] = diagramType,
                ["is_diagram"] = "true",
                ["file_format"] = Path.GetExtension(document.FileName).TrimStart('.')
            };

            var chunk = new DocumentChunk
            {
                ChunkId = $"{diagramId}_diagram",
                SourceDocumentId = diagramId,
                DocumentType = "Diagram",
                Section = "Diagram",
                Content = searchableContent,
                ChunkIndex = 0,
                MetadataJson = BuildMetadataJson(document, null, metadata),
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Created single chunk for diagram {Id} (type: {Type})", diagramId, diagramType);
            
            return await Task.FromResult(new List<DocumentChunk> { chunk });
        }

        private string ExtractDiagramType(string fileName, string content)
        {
            var name = fileName.ToLowerInvariant();
            
            if (name.Contains("sequence")) return "sequence";
            if (name.Contains("flow")) return "flow";
            if (name.Contains("class")) return "class";
            if (name.Contains("er") || name.Contains("entity")) return "entity-relationship";
            if (name.Contains("context")) return "c4-context";
            if (name.Contains("container")) return "c4-container";
            if (name.Contains("component")) return "c4-component";
            
            // Try to detect from content
            if (content.Contains("sequenceDiagram")) return "sequence";
            if (content.Contains("flowchart") || content.Contains("graph")) return "flow";
            if (content.Contains("classDiagram")) return "class";
            if (content.Contains("erDiagram")) return "entity-relationship";
            
            return "unknown";
        }

        private string BuildSearchableContent(ParsedMarkdown parsed)
        {
            var contentParts = new List<string>();

            // Add title
            var titleSection = parsed.Sections.FirstOrDefault();
            if (titleSection != null && titleSection.Level == 1)
            {
                contentParts.Add($"Diagram: {titleSection.Title}");
            }

            // Add all section content (descriptions, notes, etc.)
            foreach (var section in parsed.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Content))
                {
                    contentParts.Add(section.Content.Trim());
                }
            }

            // Extract text from diagram syntax (Mermaid)
            // This could be enhanced to parse specific diagram elements
            var fullText = parsed.FullContent;
            var lines = fullText.Split('\n')
                .Where(line => !line.TrimStart().StartsWith("```") && 
                               !line.TrimStart().StartsWith("graph") && 
                               !line.TrimStart().StartsWith("flowchart") &&
                               !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim());
            
            contentParts.AddRange(lines);

            return string.Join("\n\n", contentParts);
        }
    }
}
