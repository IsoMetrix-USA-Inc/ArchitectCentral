namespace KnowledgebaseVectoriser.Configuration;

/// <summary>
/// Configuration options for document chunking strategies.
/// Provides flexible settings to support different types of knowledge bases
/// (e.g., Architect Central, ESG Central) with varying content characteristics.
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// Maximum chunk size in tokens. Default: 512 tokens (~2000 characters)
    /// For text-embedding-ada-002, max context is 8191 tokens.
    /// </summary>
    public int MaxChunkTokens { get; set; } = 512;

    /// <summary>
    /// Minimum chunk size in tokens to prevent overly short chunks. Default: 50 tokens (~200 characters)
    /// Short chunks lack context and may reduce retrieval quality.
    /// </summary>
    public int MinChunkTokens { get; set; } = 50;

    /// <summary>
    /// Overlap size in tokens between adjacent chunks. Default: 50 tokens (~10% of max)
    /// Overlap maintains context across chunk boundaries and improves retrieval.
    /// </summary>
    public int OverlapTokens { get; set; } = 50;

    /// <summary>
    /// The OpenAI model to use for token counting. Default: "text-embedding-ada-002"
    /// Must match the embedding model used for vectorization.
    /// </summary>
    public string TokenCountingModel { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// Whether to merge consecutive short chunks below MinChunkTokens. Default: true
    /// Helps eliminate very short chunks from brief sections.
    /// </summary>
    public bool MergeShortChunks { get; set; } = true;

    /// <summary>
    /// Whether to preserve code blocks as single units (don't split). Default: true
    /// Critical for maintaining code integrity in technical documentation.
    /// </summary>
    public bool PreserveCodeBlocks { get; set; } = true;

    /// <summary>
    /// Validates configuration values and throws if invalid
    /// </summary>
    public void Validate()
    {
        if (MaxChunkTokens < 100)
            throw new ArgumentException("MaxChunkTokens must be at least 100", nameof(MaxChunkTokens));

        if (MinChunkTokens < 10)
            throw new ArgumentException("MinChunkTokens must be at least 10", nameof(MinChunkTokens));

        if (MinChunkTokens >= MaxChunkTokens)
            throw new ArgumentException("MinChunkTokens must be less than MaxChunkTokens", nameof(MinChunkTokens));

        if (OverlapTokens < 0)
            throw new ArgumentException("OverlapTokens must be non-negative", nameof(OverlapTokens));

        if (OverlapTokens >= MaxChunkTokens)
            throw new ArgumentException("OverlapTokens must be less than MaxChunkTokens", nameof(OverlapTokens));

        if (string.IsNullOrWhiteSpace(TokenCountingModel))
            throw new ArgumentException("TokenCountingModel cannot be empty", nameof(TokenCountingModel));
    }
}
