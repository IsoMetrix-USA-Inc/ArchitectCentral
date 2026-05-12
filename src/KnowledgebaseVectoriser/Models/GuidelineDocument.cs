using Microsoft.Extensions.VectorData;

namespace KnowledgebaseVectoriser.Models
{
    /// <summary>
    /// Model for Architectural Guidelines stored in the vector database.
    /// Guidelines are evolving documents that describe implementation best practices.
    /// </summary>
    public class GuidelineDocument
    {
        /// <summary>
        /// Unique identifier (format: guideline_{slug})
        /// </summary>
        [VectorStoreKey(StorageName = "guideline_id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Title of the guideline
        /// </summary>
        [VectorStoreData(StorageName = "title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Category (e.g., API Design, Data Architecture, Security)
        /// </summary>
        [VectorStoreData(StorageName = "category")]
        public string? Category { get; set; }

        /// <summary>
        /// Main sections of the guideline (JSON array of section titles)
        /// </summary>
        [VectorStoreData(StorageName = "sections")]
        public string? SectionsJson { get; set; }

        /// <summary>
        /// Number of code examples in this guideline
        /// </summary>
        [VectorStoreData(StorageName = "code_example_count")]
        public int CodeExampleCount { get; set; }

        /// <summary>
        /// Related ADRs that motivate these guidelines (comma-separated or JSON)
        /// </summary>
        [VectorStoreData(StorageName = "related_adrs")]
        public string? RelatedAdrs { get; set; }

        /// <summary>
        /// Tags for categorization
        /// </summary>
        [VectorStoreData(StorageName = "tags")]
        public string? Tags { get; set; }

        /// <summary>
        /// Full content of the guideline for embedding
        /// </summary>
        [VectorStoreData(StorageName = "full_content")]
        public string FullContent { get; set; } = string.Empty;

        /// <summary>
        /// File path relative to docs/ folder
        /// </summary>
        [VectorStoreData(StorageName = "file_path")]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Last updated timestamp from file
        /// </summary>
        [VectorStoreData(StorageName = "last_updated")]
        public DateTime? LastUpdated { get; set; }

        /// <summary>
        /// When this was indexed
        /// </summary>
        [VectorStoreData(StorageName = "indexed_at")]
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Vector embedding for semantic search (1536 dimensions for text-embedding-ada-002)
        /// </summary>
        [VectorStoreVector(
            Dimensions: 1536,
            DistanceFunction = DistanceFunction.CosineDistance,
            IndexKind = IndexKind.Hnsw,
            StorageName = "embedding")]
        public ReadOnlyMemory<float>? Embedding { get; set; }
    }
}
