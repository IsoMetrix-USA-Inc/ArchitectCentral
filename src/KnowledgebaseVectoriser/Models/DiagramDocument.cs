using Microsoft.Extensions.VectorData;

namespace KnowledgebaseVectoriser.Models
{
    /// <summary>
    /// Model for Diagrams and visual documentation stored in the vector database.
    /// Diagrams include Mermaid files, sequence diagrams, C4 diagrams, etc.
    /// </summary>
    public class DiagramDocument
    {
        /// <summary>
        /// Unique identifier (format: diagram_{slug})
        /// </summary>
        [VectorStoreKey(StorageName = "diagram_id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Name/title of the diagram
        /// </summary>
        [VectorStoreData(StorageName = "name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type of diagram (e.g., sequence, flow, class, C4-context, C4-container)
        /// </summary>
        [VectorStoreData(StorageName = "diagram_type")]
        public string? DiagramType { get; set; }

        /// <summary>
        /// Description of what the diagram shows
        /// </summary>
        [VectorStoreData(StorageName = "description")]
        public string? Description { get; set; }

        /// <summary>
        /// Related documents (ADRs, guidelines) that reference this diagram
        /// </summary>
        [VectorStoreData(StorageName = "related_docs")]
        public string? RelatedDocs { get; set; }

        /// <summary>
        /// Tags for categorization
        /// </summary>
        [VectorStoreData(StorageName = "tags")]
        public string? Tags { get; set; }

        /// <summary>
        /// Extracted text content from diagram (for embedding)
        /// Includes diagram title, labels, descriptions, comments
        /// </summary>
        [VectorStoreData(StorageName = "extracted_text")]
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>
        /// Raw diagram source (e.g., Mermaid code)
        /// </summary>
        [VectorStoreData(StorageName = "diagram_source")]
        public string? DiagramSource { get; set; }

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
