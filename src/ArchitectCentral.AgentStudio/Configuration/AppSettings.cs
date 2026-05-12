namespace ArchitectCentral.AgentStudio.Configuration;

public class AppSettings
{
    public AzureOpenAIConfig AzureOpenAI { get; set; } = new();
    public PostgreSQLConfig PostgreSQL { get; set; } = new();
    public AgentStudioConfig AgentStudio { get; set; } = new();
    public VectorStoreConfig VectorStore { get; set; } = new();
    public DocumentProcessingConfig DocumentProcessing { get; set; } = new();
}

public class AzureOpenAIConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChatDeploymentName { get; set; } = "gpt-4.1";
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-ada-002";
}

public class PostgreSQLConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class AgentStudioConfig
{
    public int MaxConversationHistory { get; set; } = 10;
    public RAGConfig RAG { get; set; } = new();
    public WorkflowConfig Workflow { get; set; } = new();
}

public class RAGConfig
{
    public int TopK { get; set; } = 5;
    public double SimilarityThreshold { get; set; } = 0.70;
}

public class WorkflowConfig
{
    public int MaxIterations { get; set; } = 3;
    public double QualityThreshold { get; set; } = 0.80;
    public int MaxKnowledgeChunks { get; set; } = 10;
}

public class VectorStoreConfig
{
    public CollectionsConfig Collections { get; set; } = new();
    public bool UseV2Schema { get; set; }
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
