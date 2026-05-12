using Azure;
using Azure.AI.OpenAI;
using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Service for generating embeddings using Azure OpenAI
    /// </summary>
    public class EmbeddingService
    {
        private readonly ILogger<EmbeddingService> _logger;
        private readonly AzureOpenAIClient _openAIClient;
        private readonly EmbeddingClient _embeddingClient;

        public EmbeddingService(
            ILogger<EmbeddingService> logger,
            string endpoint,
            string apiKey,
            string deploymentName)
        {
            _logger = logger;
            _openAIClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
            _embeddingClient = _openAIClient.GetEmbeddingClient(deploymentName);
        }

        /// <summary>
        /// Generate embeddings for a single text
        /// </summary>
        public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Attempted to generate embedding for empty text");
                return ReadOnlyMemory<float>.Empty;
            }

            try
            {
                var embedding = await _embeddingClient.GenerateEmbeddingAsync(text);
                
                _logger.LogDebug("Generated embedding of {Dimensions} dimensions for text of {Length} characters", 
                    embedding.Value.ToFloats().Length, text.Length);
                
                return embedding.Value.ToFloats().ToArray();
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure OpenAI request failed: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate embedding");
                throw;
            }
        }

        /// <summary>
        /// Generate embeddings for multiple texts in batches
        /// </summary>
        public async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsBatchAsync(
            List<string> texts,
            int batchSize = 16)
        {
            if (texts == null || texts.Count == 0)
            {
                return new List<ReadOnlyMemory<float>>();
            }

            _logger.LogInformation("Generating embeddings for {Count} texts in batches of {BatchSize}", 
                texts.Count, batchSize);

            var embeddings = new List<ReadOnlyMemory<float>>();
            var batches = texts.Chunk(batchSize).ToList();

            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i].ToList();
                
                try
                {
                    _logger.LogDebug("Processing batch {BatchNumber}/{TotalBatches} ({Count} texts)", 
                        i + 1, batches.Count, batch.Count);

                    var response = await _embeddingClient.GenerateEmbeddingsAsync(batch);

                    foreach (var item in response.Value)
                    {
                        embeddings.Add(item.ToFloats().ToArray());
                    }

                    // Small delay to respect rate limits
                    if (i < batches.Count - 1)
                    {
                        await Task.Delay(100);
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 429)
                {
                    _logger.LogWarning("Rate limit hit, waiting 5 seconds before retry...");
                    await Task.Delay(5000);
                    
                    // Retry this batch
                    var response = await _embeddingClient.GenerateEmbeddingsAsync(batch);

                    foreach (var item in response.Value)
                    {
                        embeddings.Add(item.ToFloats().ToArray());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate embeddings for batch {BatchNumber}", i + 1);
                    throw;
                }
            }

            _logger.LogInformation("Successfully generated {Count} embeddings", embeddings.Count);
            return embeddings;
        }

        /// <summary>
        /// Get embedding dimensions (should be 1536 for text-embedding-ada-002)
        /// </summary>
        public async Task<int> GetEmbeddingDimensionsAsync()
        {
            var testEmbedding = await GenerateEmbeddingAsync("test");
            return testEmbedding.Length;
        }

        /// <summary>
        /// Generate embeddings for document chunks
        /// </summary>
        public async Task<List<DocumentChunk>> GenerateChunkEmbeddingsAsync(
            List<DocumentChunk> chunks,
            int batchSize = 16)
        {
            if (chunks == null || chunks.Count == 0)
            {
                return chunks;
            }

            _logger.LogInformation("Generating embeddings for {Count} document chunks", chunks.Count);

            // Extract texts from chunks
            var texts = chunks.Select(c => c.Content).ToList();

            // Generate embeddings in batches
            var embeddings = await GenerateEmbeddingsBatchAsync(texts, batchSize);

            // Assign embeddings to chunks
            for (int i = 0; i < chunks.Count; i++)
            {
                chunks[i].Embedding = embeddings[i];
            }

            _logger.LogInformation("Successfully embedded {Count} chunks", chunks.Count);
            return chunks;
        }
    }
}
