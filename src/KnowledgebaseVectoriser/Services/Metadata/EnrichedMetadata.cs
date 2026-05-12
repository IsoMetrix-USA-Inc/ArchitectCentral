namespace KnowledgebaseVectoriser.Services.Metadata
{
    /// <summary>
    /// Enriched metadata for a chunk
    /// </summary>
    public class EnrichedMetadata
    {
        public string ChunkId { get; set; } = string.Empty;
        public string SourceDocumentId { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Status { get; set; } = "unknown";
        public string Category { get; set; } = "general";
        public string Sentiment { get; set; } = "neutral";
        public decimal QualityScore { get; set; }
        public List<string> TechnicalTags { get; set; } = new();
        public List<string> FrameworkRefs { get; set; } = new();
        public CompletenessMetadata Completeness { get; set; } = new();
    }
}
