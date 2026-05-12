namespace KnowledgebaseVectoriser.Services.Metadata
{
    /// <summary>
    /// Configuration for metadata enrichment
    /// </summary>
    public class MetadataEnrichmentConfig
    {
        /// <summary>
        /// Enable ADR status extraction
        /// </summary>
        public bool ExtractAdrStatus { get; set; } = true;

        /// <summary>
        /// Enable sentiment analysis (code examples vs anti-patterns)
        /// </summary>
        public bool AnalyzeSentiment { get; set; } = true;

        /// <summary>
        /// Enable quality score calculation
        /// </summary>
        public bool CalculateQualityScore { get; set; } = true;

        /// <summary>
        /// Enable technical tag extraction
        /// </summary>
        public bool ExtractTechnicalTags { get; set; } = true;

        /// <summary>
        /// Enable framework reference detection
        /// </summary>
        public bool DetectFrameworkRefs { get; set; } = true;

        /// <summary>
        /// Enable category classification
        /// </summary>
        public bool ClassifyCategory { get; set; } = true;

        /// <summary>
        /// Minimum quality score threshold (0.0 - 1.0)
        /// </summary>
        public decimal MinimumQualityScore { get; set; } = 0.0m;

        /// <summary>
        /// Quality score weights
        /// </summary>
        public QualityScoreWeights Weights { get; set; } = new();
    }
}
