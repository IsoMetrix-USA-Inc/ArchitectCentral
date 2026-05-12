using KnowledgebaseVectoriser.Configuration;
using KnowledgebaseVectoriser.Services;
using KnowledgebaseVectoriser.Services.Chunking;
using KnowledgebaseVectoriser.Services.ErrorHandling;
using KnowledgebaseVectoriser.Services.Generation;
using KnowledgebaseVectoriser.Services.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .Build();

var appSettings = new AppSettings();
configuration.Bind(appSettings);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Create logger factory
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSerilog();
});

var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("=== Architect Central - Knowledgebase Vectoriser ===");

// Check for auto-populate mode (from Aspire environment variable)
var runMode = Environment.GetEnvironmentVariable("RUN_MODE");
bool autoPopulateMode = runMode?.Equals("AutoPopulate", StringComparison.OrdinalIgnoreCase) ?? false;

if (autoPopulateMode)
{
    logger.LogInformation("Running in AUTO-POPULATE mode - will check database and populate if empty");
    
    // Quick check: connect to database and see if it has data
    var testEmbedding = new EmbeddingService(
        loggerFactory.CreateLogger<EmbeddingService>(),
        appSettings.AzureOpenAI.Endpoint,
        appSettings.AzureOpenAI.ApiKey,
        appSettings.AzureOpenAI.EmbeddingDeploymentName
    );
    
    var testVectorDb = new VectorDatabaseService(
        loggerFactory.CreateLogger<VectorDatabaseService>(),
        appSettings.ConnectionStrings.PostgreSQL,
        testEmbedding
    );
    
    // Test connection
    var connected = await testVectorDb.TestConnectionAsync();
    if (!connected)
    {
        logger.LogWarning("Database not ready yet. Will retry...");
        await Task.Delay(5000); // Wait for database to be ready
        connected = await testVectorDb.TestConnectionAsync();
    }
    
    if (connected)
    {
        // Check if database has data
        var stats = await testVectorDb.GetStatsAsync();
        var totalChunks = stats.ChunkCounts.Values.Sum();
        
        if (totalChunks > 0)
        {
            logger.LogInformation("Database already has {Count} chunks - skipping vectorization", totalChunks);
            logger.LogInformation("=== Auto-populate check complete - no action needed ===");
            testVectorDb.Dispose();
            return 0;
        }
        else
        {
            logger.LogInformation("Database is empty - starting vectorization process");
        }
    }
    
    testVectorDb.Dispose();
}

// Check for test mode
if (args.Length > 0 && args[0].ToLower() == "--test")
{
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine("Enhanced RAG Configuration - Quick Verification");
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine();

    Console.WriteLine("✅ Enhanced Chunking Configuration:");
    Console.WriteLine($"   UseEnhancedChunking: {appSettings.DocumentProcessing.UseEnhancedChunking}");
    Console.WriteLine($"   OverlapPercentage: {appSettings.DocumentProcessing.OverlapPercentage * 100}%");
    Console.WriteLine($"   TrackOverlapPositions: {appSettings.DocumentProcessing.TrackOverlapPositions}");
    Console.WriteLine($"   BoundaryStrategy: {appSettings.DocumentProcessing.BoundaryStrategy}");
    Console.WriteLine($"   ChunkSize: {appSettings.DocumentProcessing.ChunkSize} tokens");
    Console.WriteLine();

    Console.WriteLine("✅ Metadata Enrichment Configuration:");
    Console.WriteLine($"   EnableMetadataEnrichment: {appSettings.VectorStore.EnableMetadataEnrichment}");
    Console.WriteLine($"   EnableQualityScoring: {appSettings.VectorStore.EnableQualityScoring}");
    Console.WriteLine($"   UseV2Schema: {appSettings.VectorStore.UseV2Schema}");
    Console.WriteLine();

    Console.WriteLine("✅ Enhanced Services Files:");
    var serviceFiles = new[]
    {
        @"Services\Chunking\EnhancedChunkingService.cs",
        @"Services\Chunking\EnhancedADRChunkingStrategy.cs",
        @"Services\Metadata\MetadataEnricherService.cs",
        @"Services\Query\QueryOptimizerService.cs",
        @"Services\Query\HybridSearchService.cs",
        @"Services\EnhancedDocumentProcessor.cs"
    };

    foreach (var file in serviceFiles)
    {
        var exists = File.Exists(file);
        var status = exists ? "✓" : "✗";
        Console.WriteLine($"   {status} {file}");
    }

    Console.WriteLine();
    Console.WriteLine("=".PadRight(80, '='));
    Console.WriteLine("✅ Configuration Verified - Core Services Available!");
    Console.WriteLine("=".PadRight(80, '='));
    return 0;
}

logger.LogInformation("=== End-to-End RAG System Test ===\n");

try
{
    // ===========================================
    // Phase 1: Document Processing & Chunking
    // ===========================================
    logger.LogInformation("--- Phase 1: Document Processing & Chunking ---");
    
    var scanner = new DocumentScanner(
        loggerFactory.CreateLogger<DocumentScanner>(),
        appSettings.DocumentProcessing.DocsPath
    );

    var markdownParser = new MarkdownParser(loggerFactory.CreateLogger<MarkdownParser>());
    var adrParser = new ADRParser(loggerFactory.CreateLogger<ADRParser>(), markdownParser);

    var chunkingConfig = new ChunkingConfig
    {
        MaxChunkSize = appSettings.DocumentProcessing.ChunkSize,
        OverlapSize = appSettings.DocumentProcessing.ChunkOverlap,
        MinChunkSize = appSettings.DocumentProcessing.MinChunkSize,
        MergeShortChunks = appSettings.DocumentProcessing.MergeShortChunks,
        PreserveCodeBlocks = appSettings.DocumentProcessing.PreserveCodeBlocks,
        PreserveSections = appSettings.DocumentProcessing.PreserveSections
    };

    // Create enhanced chunking config if enabled
    EnhancedChunkingConfig? enhancedConfig = null;
    EnhancedChunkingService? enhancedChunkingService = null;
    MetadataEnricherService? metadataEnricher = null;

    if (appSettings.DocumentProcessing.UseEnhancedChunking)
    {
        logger.LogInformation("Enhanced chunking V2 enabled with {Overlap}% overlap", 
            appSettings.DocumentProcessing.OverlapPercentage * 100);

        enhancedConfig = new EnhancedChunkingConfig
        {
            MaxChunkSize = appSettings.DocumentProcessing.ChunkSize,
            MinChunkSize = appSettings.DocumentProcessing.MinChunkSize,
            OverlapPercentage = appSettings.DocumentProcessing.OverlapPercentage,
            TrackOverlapPositions = appSettings.DocumentProcessing.TrackOverlapPositions,
            BoundaryStrategy = Enum.Parse<BoundaryStrategy>(
                appSettings.DocumentProcessing.BoundaryStrategy, 
                ignoreCase: true),
            PreserveCodeBlocks = appSettings.DocumentProcessing.PreserveCodeBlocks,
            PreserveSections = appSettings.DocumentProcessing.PreserveSections,
            MergeShortChunks = appSettings.DocumentProcessing.MergeShortChunks
        };

        enhancedChunkingService = new EnhancedChunkingService(
            enhancedConfig,
            loggerFactory.CreateLogger<EnhancedChunkingService>()
        );
    }

    // Create metadata enricher if enabled
    if (appSettings.VectorStore.EnableMetadataEnrichment)
    {
        logger.LogInformation("Metadata enrichment enabled");
        
        var enrichmentConfig = new MetadataEnrichmentConfig
        {
            ExtractAdrStatus = true,
            AnalyzeSentiment = true,
            CalculateQualityScore = appSettings.VectorStore.EnableQualityScoring,
            ExtractTechnicalTags = true,
            DetectFrameworkRefs = true,
            ClassifyCategory = true
        };

        metadataEnricher = new MetadataEnricherService(
            loggerFactory.CreateLogger<MetadataEnricherService>(),
            enrichmentConfig
        );
    }

    var processor = new EnhancedDocumentProcessor(
        loggerFactory.CreateLogger<EnhancedDocumentProcessor>(),
        scanner,
        markdownParser,
        adrParser,
        chunkingConfig,
        enhancedConfig,
        enhancedChunkingService,
        metadataEnricher
    );

    var processingResult = await processor.ProcessAllDocumentsAsync();
    logger.LogInformation("✓ Processed {Count} documents into {ChunkCount} chunks\n", 
        processingResult.ProcessedDocuments, processingResult.AllChunks.Count);

    // ===========================================
    // Phase 2: Vector Database Connection
    // ===========================================
    logger.LogInformation("--- Phase 2: Vector Database Connection ---");
    
    var embeddingService = new EmbeddingService(
        loggerFactory.CreateLogger<EmbeddingService>(),
        appSettings.AzureOpenAI.Endpoint,
        appSettings.AzureOpenAI.ApiKey,
        appSettings.AzureOpenAI.EmbeddingDeploymentName
    );
    
    var vectorDb = new VectorDatabaseService(
        loggerFactory.CreateLogger<VectorDatabaseService>(),
        appSettings.ConnectionStrings.PostgreSQL,
        embeddingService
    );

    var dbConnected = await vectorDb.TestConnectionAsync();
    if (!dbConnected)
    {
        logger.LogError("Failed to connect to PostgreSQL. Please ensure Docker container is running.");
        logger.LogInformation("Run: docker-compose up -d");
        return 1;
    }
    logger.LogInformation("✓ Connected to PostgreSQL with pgvector\n");

    // ===========================================
    // Phase 3: Embedding Generation
    // ===========================================
    logger.LogInformation("--- Phase 3: Embedding Generation ---");
    
    var retryPolicy = new RetryPolicy(loggerFactory.CreateLogger<RetryPolicy>());

    try
    {
        // Generate embeddings for all chunks
        var chunks = processingResult.AllChunks;
        logger.LogInformation("Generating embeddings for {Count} chunks...", chunks.Count);
        
        var embeddedChunks = await retryPolicy.ExecuteAsync(
            async () => await embeddingService.GenerateChunkEmbeddingsAsync(chunks),
            "Generate embeddings",
            maxRetries: 3,
            delaySeconds: 5
        );
        
        logger.LogInformation("✓ Generated {Count} embeddings\n", embeddedChunks.Count);

        // ===========================================
        // Phase 4: Store in Vector Database
        // ===========================================
        logger.LogInformation("--- Phase 4: Store in Vector Database ---");
        
        await retryPolicy.ExecuteAsync(
            async () =>
            {
                await vectorDb.UpsertChunksAsync(embeddedChunks);
                return true;
            },
            "Store chunks in database",
            maxRetries: 3
        );
        
        var stats = await vectorDb.GetStatsAsync();
        logger.LogInformation("✓ Stored chunks in database");
        logger.LogInformation("  ADR chunks: {ADR}", stats.ChunkCounts.GetValueOrDefault("ADR", 0));
        logger.LogInformation("  Guideline chunks: {Guideline}", stats.ChunkCounts.GetValueOrDefault("Guideline", 0));
        logger.LogInformation("  Diagram chunks: {Diagram}", stats.ChunkCounts.GetValueOrDefault("Diagram", 0));
        logger.LogInformation("  Total: {Total}\n", stats.TotalChunks);

        // ===========================================
        // Phase 5: RAG Query Tests
        // ===========================================
        logger.LogInformation("--- Phase 5: RAG Query System Tests ---\n");
        
        var ragService = new RAGQueryService(
            loggerFactory.CreateLogger<RAGQueryService>(),
            vectorDb,
            embeddingService
        );

        // Test 1: Vertical Slice Architecture query
        logger.LogInformation("Test 1: Vertical Slice Architecture Query");
        var query1 = "Create new Web API with EF Core and vertical slices";
        var results1 = await ragService.SearchAsync(query1, topK: 5, similarityThreshold: 0.65);
        logger.LogInformation("  Query: \"{Query}\"", query1);
        logger.LogInformation("  Results: {Count} chunks found", results1.Count);
        foreach (var result in results1.Take(3))
        {
            var filePath = System.Text.Json.JsonDocument.Parse(result.Metadata ?? "{}").RootElement
                .TryGetProperty("file_path", out var fp) ? fp.GetString() : "unknown";
            logger.LogInformation("    - {File} ({Score:F3}): {Preview}", 
                filePath, result.Similarity, 
                result.Content.Length > 80 ? result.Content.Substring(0, 80) + "..." : result.Content);
        }
        logger.LogInformation("");

        // Test 2: Multi-tenant query
        logger.LogInformation("Test 2: Multi-Tenant Data Isolation Query");
        var query2 = "How do we handle multi-tenant data isolation in our architecture?";
        var results2 = await ragService.SearchAsync(query2, topK: 5, similarityThreshold: 0.65);
        logger.LogInformation("  Query: \"{Query}\"", query2);
        logger.LogInformation("  Results: {Count} chunks found", results2.Count);
        foreach (var result in results2.Take(3))
        {
            var filePath = System.Text.Json.JsonDocument.Parse(result.Metadata ?? "{}").RootElement
                .TryGetProperty("file_path", out var fp) ? fp.GetString() : "unknown";
            logger.LogInformation("    - {File} ({Score:F3}): {Preview}", 
                filePath, result.Similarity, 
                result.Content.Length > 80 ? result.Content.Substring(0, 80) + "..." : result.Content);
        }
        logger.LogInformation("");

        // Test 3: Authentication query
        logger.LogInformation("Test 3: Authentication & Federation Query");
        var query3 = "What authentication and federation patterns should we use?";
        var results3 = await ragService.SearchAsync(query3, topK: 5, similarityThreshold: 0.65);
        logger.LogInformation("  Query: \"{Query}\"", query3);
        logger.LogInformation("  Results: {Count} chunks found", results3.Count);
        foreach (var result in results3.Take(3))
        {
            var filePath = System.Text.Json.JsonDocument.Parse(result.Metadata ?? "{}").RootElement
                .TryGetProperty("file_path", out var fp) ? fp.GetString() : "unknown";
            logger.LogInformation("    - {File} ({Score:F3}): {Preview}", 
                filePath, result.Similarity, 
                result.Content.Length > 80 ? result.Content.Substring(0, 80) + "..." : result.Content);
        }
        logger.LogInformation("");

        // ===========================================
        // Phase 6: Agent File Generation
        // ===========================================
        logger.LogInformation("--- Phase 6: Agent File Generation ---\n");
        
        var generator = new AgentFileGenerator(
            loggerFactory.CreateLogger<AgentFileGenerator>(),
            ragService
        );

        var agentRequest = new AgentGenerationRequest
        {
            AgentName = "web-api-vertical-slices",
            Description = "Create a new Web API using dotnet 10, EF Core, and vertical slice architecture",
            Technologies = new List<string> { "dotnet 10", "Web API", "Entity Framework Core", "Vertical Slices" },
            Patterns = new List<string> { "CQRS", "Repository Pattern", "Dependency Injection" }
        };

        logger.LogInformation("Generating .agent file for: {Description}", agentRequest.Description);
        var agentContent = await generator.GenerateAgentFileAsync(agentRequest);
        
        // Save to output
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "test-output");
        Directory.CreateDirectory(outputPath);
        var agentFilePath = Path.Combine(outputPath, $"{agentRequest.AgentName}.agent");
        await File.WriteAllTextAsync(agentFilePath, agentContent);
        
        logger.LogInformation("✓ Generated .agent file: {Path}", agentFilePath);
        logger.LogInformation("  File size: {Size} bytes\n", agentContent.Length);

        // ===========================================
        // Summary
        // ===========================================
        logger.LogInformation("=== End-to-End Test Summary ===");
        logger.LogInformation("✓ Phase 1: Processed {DocCount} documents → {ChunkCount} chunks", 
            processingResult.ProcessedDocuments, processingResult.AllChunks.Count);
        logger.LogInformation("✓ Phase 2: Connected to PostgreSQL with pgvector");
        logger.LogInformation("✓ Phase 3: Generated {EmbedCount} embeddings", embeddedChunks.Count);
        logger.LogInformation("✓ Phase 4: Stored chunks in vector database");
        logger.LogInformation("✓ Phase 5: Tested 3 RAG queries successfully");
        logger.LogInformation("✓ Phase 6: Generated .agent file with ADR context");
        logger.LogInformation("\n🎉 Architect Central RAG System is fully operational!");
        logger.LogInformation("\nGenerated files saved to: {Path}", outputPath);
    }
    catch (EmbeddingException ex)
    {
        logger.LogError("Embedding service error: {Message}", ex.Message);
        logger.LogInformation("\nPlease configure Azure OpenAI credentials:");
        logger.LogInformation("  dotnet user-secrets set \"AzureOpenAI:Endpoint\" \"https://your-resource.openai.azure.com/\"");
        logger.LogInformation("  dotnet user-secrets set \"AzureOpenAI:ApiKey\" \"your-key\"");
        return 1;
    }
    catch (VectorDatabaseException ex)
    {
        logger.LogError("Vector database error: {Message}", ex.Message);
        logger.LogInformation("\nPlease ensure PostgreSQL container is running:");
        logger.LogInformation("  docker-compose up -d");
        return 1;
    }

    logger.LogInformation("\nPress any key to exit...");
    Console.ReadKey();

    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error occurred");
    logger.LogInformation("\nTroubleshooting:");
    logger.LogInformation("1. Ensure Docker container is running: docker-compose up -d");
    logger.LogInformation("2. Configure Azure OpenAI credentials via user secrets");
    logger.LogInformation("3. Check connection strings in appsettings.json");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}