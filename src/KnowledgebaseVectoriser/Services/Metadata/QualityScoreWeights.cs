namespace KnowledgebaseVectoriser.Services.Metadata
{
    /// <summary>
    /// Weights for quality score calculation
    /// </summary>
    public class QualityScoreWeights
    {
        public decimal Length { get; set; } = 0.30m;
        public decimal Structure { get; set; } = 0.25m;
        public decimal Examples { get; set; } = 0.20m;
        public decimal Completeness { get; set; } = 0.15m;
        public decimal Clarity { get; set; } = 0.10m;
    }
}
