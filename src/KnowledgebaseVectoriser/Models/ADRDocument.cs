using Microsoft.Extensions.VectorData;

namespace KnowledgebaseVectoriser.Models
{
    /// <summary>
    /// Model for Architecture Decision Records (ADRs) stored in the vector database.
    /// ADRs are immutable records of significant architectural decisions.
    /// </summary>
    public class ADRDocument
    {
        /// <summary>
        /// Unique identifier (format: adr_{number})
        /// </summary>
        [VectorStoreKey(StorageName = "adr_id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// ADR number (e.g., 0001, 0002)
        /// </summary>
        [VectorStoreData(StorageName = "adr_number")]
        public int Number { get; set; }

        /// <summary>
        /// Title of the ADR
        /// </summary>
        [VectorStoreData(StorageName = "title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Status: Proposed, Accepted, Deprecated, Superseded
        /// </summary>
        [VectorStoreData(StorageName = "status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Date of the decision
        /// </summary>
        [VectorStoreData(StorageName = "date")]
        public DateTime? Date { get; set; }

        /// <summary>
        /// Context section - why this decision is needed
        /// </summary>
        [VectorStoreData(StorageName = "context")]
        public string? Context { get; set; }

        /// <summary>
        /// Decision section - what was decided
        /// </summary>
        [VectorStoreData(StorageName = "decision")]
        public string? Decision { get; set; }

        /// <summary>
        /// Consequences section - impacts and trade-offs
        /// </summary>
        [VectorStoreData(StorageName = "consequences")]
        public string? Consequences { get; set; }

        /// <summary>
        /// Alternatives section - options that were considered
        /// </summary>
        [VectorStoreData(StorageName = "alternatives")]
        public string? Alternatives { get; set; }

        /// <summary>
        /// Related ADRs (comma-separated IDs or JSON array)
        /// </summary>
        [VectorStoreData(StorageName = "related_adrs")]
        public string? RelatedAdrs { get; set; }

        /// <summary>
        /// Tags for categorization (comma-separated or JSON array)
        /// </summary>
        [VectorStoreData(StorageName = "tags")]
        public string? Tags { get; set; }

        /// <summary>
        /// Full content of the ADR for embedding
        /// </summary>
        [VectorStoreData(StorageName = "full_content")]
        public string FullContent { get; set; } = string.Empty;

        /// <summary>
        /// File path relative to docs/ folder
        /// </summary>
        [VectorStoreData(StorageName = "file_path")]
        public string FilePath { get; set; } = string.Empty;

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
