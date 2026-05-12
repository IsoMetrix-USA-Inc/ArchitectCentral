using Microsoft.Extensions.VectorData;
using System.Text.Json.Serialization;

namespace KnowledgebaseVectoriser.Models
{
    /// <summary>
    /// Base model for all chunked content stored in the vector database.
    /// Represents a semantically meaningful piece of a document with metadata.
    /// </summary>
    public class DocumentChunk
    {
        /// <summary>
        /// Unique identifier for this chunk (format: {doc_type}_{doc_id}_{chunk_index})
        /// </summary>
        [VectorStoreKey(StorageName = "chunk_id")]
        public string ChunkId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the source document this chunk came from
        /// </summary>
        [VectorStoreData(StorageName = "source_doc_id")]
        public string SourceDocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Type of document (ADR, Guideline, Diagram)
        /// </summary>
        [VectorStoreData(StorageName = "doc_type")]
        public string DocumentType { get; set; } = string.Empty;

        /// <summary>
        /// Section or part of the document this chunk represents
        /// </summary>
        [VectorStoreData(StorageName = "section")]
        public string? Section { get; set; }

        /// <summary>
        /// The actual text content of this chunk
        /// </summary>
        [VectorStoreData(StorageName = "content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Chunk sequence number within the source document
        /// </summary>
        [VectorStoreData(StorageName = "chunk_index")]
        public int ChunkIndex { get; set; }

        /// <summary>
        /// Additional metadata as JSON (tags, relationships, etc.)
        /// </summary>
        [VectorStoreData(StorageName = "metadata")]
        public string? MetadataJson { get; set; }

        /// <summary>
        /// When this chunk was created/indexed
        /// </summary>
        [VectorStoreData(StorageName = "created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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
