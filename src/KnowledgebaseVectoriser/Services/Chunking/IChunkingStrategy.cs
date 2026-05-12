using KnowledgebaseVectoriser.Models;
using TiktokenSharp;

namespace KnowledgebaseVectoriser.Services.Chunking
{
    /// <summary>
    /// Strategy interface for chunking different document types
    /// </summary>
    public interface IChunkingStrategy
    {
        /// <summary>
        /// Chunk a document into semantically meaningful pieces
        /// </summary>
        /// <param name="document">Document to chunk</param>
        /// <param name="content">Document content</param>
        /// <param name="parsedMarkdown">Parsed markdown structure</param>
        /// <returns>List of document chunks</returns>
        Task<List<DocumentChunk>> ChunkAsync(DocumentInfo document, string content, ParsedMarkdown parsedMarkdown);
    }

    /// <summary>
    /// Configuration for chunking behavior
    /// </summary>
    public class ChunkingConfig
    {
        public int MaxChunkSize { get; set; } = 600;
        public int OverlapSize { get; set; } = 100;
        public int MinChunkSize { get; set; } = 50;
        public bool PreserveCodeBlocks { get; set; } = true;
        public bool PreserveSections { get; set; } = true;
        public bool MergeShortChunks { get; set; } = true;
    }

    /// <summary>
    /// Base class for chunking strategies with common utilities
    /// </summary>
    public abstract class BaseChunkingStrategy : IChunkingStrategy
    {
        protected readonly ChunkingConfig _config;
        private readonly TikToken _tokenEncoder;

        protected BaseChunkingStrategy(ChunkingConfig config)
        {
            _config = config;
            // Initialize tokenizer for text-embedding-ada-002 model
            _tokenEncoder = TikToken.EncodingForModel("text-embedding-ada-002");
        }

        public abstract Task<List<DocumentChunk>> ChunkAsync(DocumentInfo document, string content, ParsedMarkdown parsedMarkdown);

        /// <summary>
        /// Create a chunk ID
        /// </summary>
        protected string CreateChunkId(string documentId, int chunkIndex)
        {
            return $"{documentId}_chunk_{chunkIndex:D3}";
        }

        /// <summary>
        /// Get accurate token count using tiktoken encoder (matches OpenAI's token counting)
        /// </summary>
        protected int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            try
            {
                return _tokenEncoder.Encode(text).Count;
            }
            catch
            {
                // Fallback to character estimation if encoding fails
                return text.Length / 4;
            }
        }

        /// <summary>
        /// Split text into chunks with overlap using token-accurate counting
        /// </summary>
        protected List<string> SplitWithOverlap(string text, int maxSize, int overlapSize)
        {
            var chunks = new List<string>();
            var words = text.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            var currentChunk = new List<string>();
            int currentTokens = 0;

            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i];
                var testChunk = string.Join(" ", currentChunk) + (currentChunk.Count > 0 ? " " : "") + word;
                var testTokens = EstimateTokenCount(testChunk);

                if (testTokens > maxSize && currentChunk.Count > 0)
                {
                    // Save current chunk
                    var chunkText = string.Join(" ", currentChunk);
                    chunks.Add(chunkText);

                    // Create overlap for next chunk
                    var overlapWords = new List<string>();
                    var overlapText = "";
                    
                    for (int j = currentChunk.Count - 1; j >= 0; j--)
                    {
                        var testOverlap = currentChunk[j] + (overlapText.Length > 0 ? " " : "") + overlapText;
                        var overlapTokenCount = EstimateTokenCount(testOverlap);
                        
                        if (overlapTokenCount <= overlapSize)
                        {
                            overlapWords.Insert(0, currentChunk[j]);
                            overlapText = testOverlap;
                        }
                        else
                        {
                            break;
                        }
                    }

                    currentChunk = overlapWords;
                    currentTokens = EstimateTokenCount(overlapText);
                }

                currentChunk.Add(word);
                currentTokens = testTokens;
            }

            // Add final chunk
            if (currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));
            }

            // Merge short chunks if configured
            if (_config.MergeShortChunks)
            {
                chunks = MergeShortChunks(chunks, _config.MinChunkSize);
            }

            return chunks;
        }

        /// <summary>
        /// Merge consecutive chunks that are below the minimum token threshold
        /// </summary>
        protected List<string> MergeShortChunks(List<string> chunks, int minTokens)
        {
            if (chunks.Count <= 1)
                return chunks;

            var merged = new List<string>();
            var pendingMerge = new List<string>();

            foreach (var chunk in chunks)
            {
                var tokenCount = EstimateTokenCount(chunk);
                
                if (tokenCount < minTokens)
                {
                    // Chunk is too short, add to pending merge
                    pendingMerge.Add(chunk);
                }
                else
                {
                    // Chunk is acceptable size
                    if (pendingMerge.Count > 0)
                    {
                        // Merge pending short chunks with this one
                        var combinedChunk = string.Join("\n\n", pendingMerge) + "\n\n" + chunk;
                        merged.Add(combinedChunk);
                        pendingMerge.Clear();
                    }
                    else
                    {
                        merged.Add(chunk);
                    }
                }
            }

            // Handle any remaining short chunks at the end
            if (pendingMerge.Count > 0)
            {
                if (merged.Count > 0)
                {
                    // Merge with the last accepted chunk
                    var lastChunk = merged[merged.Count - 1];
                    merged[merged.Count - 1] = lastChunk + "\n\n" + string.Join("\n\n", pendingMerge);
                }
                else
                {
                    // All chunks are short, just join them
                    merged.Add(string.Join("\n\n", pendingMerge));
                }
            }

            return merged;
        }

        /// <summary>
        /// Build metadata JSON for a chunk
        /// </summary>
        protected string BuildMetadataJson(DocumentInfo document, MarkdownSection? section, Dictionary<string, string>? additionalMetadata = null)
        {
            var metadata = new Dictionary<string, object>
            {
                ["file_name"] = document.FileName,
                ["file_path"] = document.RelativePath,
                ["doc_type"] = document.Type.ToString(),
                ["last_modified"] = document.LastModified.ToString("O")
            };

            if (section != null)
            {
                metadata["section_title"] = section.Title;
                metadata["section_level"] = section.Level;
                metadata["has_code_blocks"] = section.CodeBlocks.Count > 0;
                metadata["code_block_count"] = section.CodeBlocks.Count;
            }

            if (additionalMetadata != null)
            {
                foreach (var kvp in additionalMetadata)
                {
                    metadata[kvp.Key] = kvp.Value;
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(metadata);
        }

        /// <summary>
        /// Merge DocumentChunk objects that are below the minimum token threshold
        /// This is applied at the strategy level after all chunks are created
        /// </summary>
        protected List<DocumentChunk> MergeShortDocumentChunks(List<DocumentChunk> chunks, int minTokens)
        {
            if (chunks.Count <= 1 || !_config.MergeShortChunks)
                return chunks;

            var merged = new List<DocumentChunk>();
            DocumentChunk? pendingMerge = null;

            foreach (var chunk in chunks)
            {
                var tokenCount = EstimateTokenCount(chunk.Content);
                
                if (tokenCount < minTokens)
                {
                    // Chunk is too short
                    if (pendingMerge == null)
                    {
                        pendingMerge = chunk;
                    }
                    else
                    {
                        // Merge with pending
                        pendingMerge = MergeTwoChunks(pendingMerge, chunk);
                    }
                }
                else
                {
                    // Chunk is acceptable size
                    if (pendingMerge != null)
                    {
                        // Merge pending short chunk with this one
                        var mergedChunk = MergeTwoChunks(pendingMerge, chunk);
                        merged.Add(mergedChunk);
                        pendingMerge = null;
                    }
                    else
                    {
                        merged.Add(chunk);
                    }
                }
            }

            // Handle any remaining pending chunk at the end
            if (pendingMerge != null)
            {
                if (merged.Count > 0)
                {
                    // Merge with the last accepted chunk
                    var lastChunk = merged[merged.Count - 1];
                    merged[merged.Count - 1] = MergeTwoChunks(lastChunk, pendingMerge);
                }
                else
                {
                    // Only short chunks exist, keep the pending one
                    merged.Add(pendingMerge);
                }
            }

            return merged;
        }

        /// <summary>
        /// Merge two DocumentChunk objects into one
        /// </summary>
        private DocumentChunk MergeTwoChunks(DocumentChunk first, DocumentChunk second)
        {
            return new DocumentChunk
            {
                ChunkId = first.ChunkId, // Keep first chunk's ID
                SourceDocumentId = first.SourceDocumentId,
                DocumentType = first.DocumentType,
                Section = first.Section,
                Content = first.Content + "\n\n" + second.Content,
                ChunkIndex = first.ChunkIndex,
                MetadataJson = first.MetadataJson, // Keep first chunk's metadata
                CreatedAt = first.CreatedAt,
                Embedding = null // Will be regenerated
            };
        }
    }
}
