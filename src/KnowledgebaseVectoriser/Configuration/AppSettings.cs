namespace KnowledgebaseVectoriser.Configuration
{
    /// <summary>
    /// Configuration settings for the application
    /// </summary>
    public class AppSettings
    {
        public ConnectionStringsConfig ConnectionStrings { get; set; } = new();
        public AzureOpenAIConfig AzureOpenAI { get; set; } = new();
        public VectorStoreConfig VectorStore { get; set; } = new();
        public DocumentProcessingConfig DocumentProcessing { get; set; } = new();
    }

    public class ConnectionStringsConfig
    {
        public string PostgreSQL { get; set; } = string.Empty;
    }

    public class AzureOpenAIConfig
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string EmbeddingDeploymentName { get; set; } = string.Empty;
    }

    public class VectorStoreConfig
    {
        public CollectionsConfig Collections { get; set; } = new();
        public bool UseV2Schema { get; set; } = false;
        public bool EnableMetadataEnrichment { get; set; } = true;
        public bool EnableQualityScoring { get; set; } = true;
    }

    public class CollectionsConfig
    {
        public string ADRs { get; set; } = "adr_collection";
        public string Guidelines { get; set; } = "guideline_collection";
        public string Diagrams { get; set; } = "diagram_collection";
    }

    public class DocumentProcessingConfig
    {
        public string DocsPath { get; set; } = "../../docs";
        public int ChunkSize { get; set; } = 600;
        public int ChunkOverlap { get; set; } = 100;
        public int MinChunkSize { get; set; } = 50;
        public bool MergeShortChunks { get; set; } = true;
        
        // Enhanced chunking V2 settings
        public bool UseEnhancedChunking { get; set; } = true;
        public double OverlapPercentage { get; set; } = 0.15;
        public bool TrackOverlapPositions { get; set; } = true;
        public string BoundaryStrategy { get; set; } = "Semantic";
        public bool PreserveCodeBlocks { get; set; } = true;
        public bool PreserveSections { get; set; } = true;
    }
}
