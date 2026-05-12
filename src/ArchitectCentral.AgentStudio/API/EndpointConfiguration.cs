using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Codebase;
using ArchitectCentral.AgentStudio.Models.Requests;
using ArchitectCentral.AgentStudio.Models.Showcase;
using ArchitectCentral.AgentStudio.Models.Workflow;
using ArchitectCentral.AgentStudio.Services;
using ArchitectCentral.AgentStudio.Workflows;
using Microsoft.Agents.AI.DevUI;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.AI;
using Npgsql;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ArchitectCentral.AgentStudio.API;

/// <summary>
/// Configures all API endpoints for the Agent Studio
/// Keeps Program.cs clean by extracting endpoint mapping logic
/// </summary>
public static class EndpointConfiguration
{
    /// <summary>
    /// Map all API endpoints to the application
    /// </summary>
    public static void MapEndpoints(WebApplication app, AppSettings appSettings)
    {
        // Error handling endpoint
        MapErrorEndpoint(app);

        // Health check endpoint
        app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

        // Pgvector test endpoint
        MapPgvectorTestEndpoint(app, appSettings);

        // RAG query endpoint
        MapRagQueryEndpoint(app);

        // Test Enhanced RAG endpoint
        MapTestEnhancedRagEndpoint(app);

        // Agent generation endpoint
        MapAgentGenerationEndpoint(app);

        // Workflow-based generation endpoint
        MapWorkflowGenerationEndpoint(app, appSettings);

        // Showcase workflow endpoint (for demo)
        MapShowcaseWorkflowEndpoint(app, appSettings);

        // Enhanced RAG endpoint (new)
        MapEnhancedRAGEndpoint(app);

        // Knowledge Synthesis endpoint (new)
        MapKnowledgeSynthesisEndpoint(app);

        // Map Agent Framework DevUI endpoints
        app.MapDevUI();

        // Map OpenAI-compatible endpoints for DevUI
        app.MapOpenAIResponses();
        app.MapOpenAIConversations();
    }

    private static void MapErrorEndpoint(WebApplication app)
    {
        app.MapGet("/error", (HttpContext context) =>
        {
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionFeature?.Error;

            var problemDetails = new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "An error occurred",
                status = 500,
                detail = exception?.Message ?? "An unexpected error occurred",
                traceId = Activity.Current?.Id ?? context.TraceIdentifier
            };

            return Results.Json(problemDetails, statusCode: 500);
        });
    }

    private static void MapRagQueryEndpoint(WebApplication app)
    {
        app.MapPost("/api/query", async (RAGQueryService ragService, QueryRequest request) =>
        {
            // Validate the request
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "errors", errors.ToArray() }
                });
            }

            var results = await ragService.QueryAsync(request.Query);
            return Results.Ok(new { query = request.Query, results, count = results.Count });
        });
    }

    private static void MapAgentGenerationEndpoint(WebApplication app)
    {
        app.MapPost("/api/generate", async (AgentGeneratorService agentService, CodebaseRequest request) =>
        {
            // Validate the request
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "errors", errors.ToArray() }
                });
            }

            var artifacts = await agentService.GenerateArtifactsAsync(request);
            return Results.Ok(new { status = "success", artifacts });
        });
    }

    private static void MapWorkflowGenerationEndpoint(WebApplication app, AppSettings appSettings)
    {
        app.MapPost("/api/workflow/generate", async (
            IChatClient chatClient,
            RAGQueryService ragService,
            WorkflowGenerationRequest request) =>
        {
            // Validate the request
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
            {
                var errors = validationResults.Select(vr => vr.ErrorMessage).ToList();
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "errors", errors.ToArray() }
                });
            }

            // Step 1: Analyze codebase
            var codebaseMetadata = new CodebaseRequest
            {
                Name = request.ProjectName ?? "Unknown",
                Description = request.Description ?? "",
                Runtime = request.Runtime ?? "",
                Framework = request.Framework ?? "",
                Dependencies = (request.KeyDependencies ?? new List<string>()).Select(d => new Dependency { Name = d, Version = "latest" }).ToList(),
                RepositoryPath = request.RepositoryPath ?? ""
            };

            // Step 2: Query relevant architecture patterns
            var architectureQuery = $"{request.Runtime} {request.Framework} best practices architecture patterns";
            var ragResults = await ragService.QueryAsync(architectureQuery);

            // Step 3: Build discovery state
            var discovery = new DiscoveryState
            {
                Codebase = codebaseMetadata,
                RelevantPatterns = ragResults,
                Existing = null, // First-time generation
                Changes = null
            };

            var context = new GenerationContext
            {
                CodebaseInfo = new CodebaseRequest
                {
                    Name = request.ProjectName,
                    Runtime = request.Runtime,
                    Framework = request.Framework,
                    Description = request.Description
                },
                ArchitecturePatterns = ragResults,
                Existing = null,
                IterationCounts = new Dictionary<string, int>(),
                QualityMetrics = new Dictionary<string, double>()
            };

            // Step 4: Execute workflow (let exceptions propagate to middleware)
            var result = await AgentFileWorkflow.ExecuteAsync(discovery, context, chatClient, appSettings);

            return Results.Ok(new
            {
                status = "success",
                workflow = "Writer-Critic",
                result = new
                {
                    type = result.Type,
                    name = result.Name,
                    content = result.Content,
                    qualityScore = result.QualityScore,
                    iteration = result.Iteration,
                    approved = result.Approved,
                    feedback = result.Feedback
                }
            });
        });
    }

    private static void MapShowcaseWorkflowEndpoint(WebApplication app, AppSettings appSettings)
    {
        app.MapPost("/api/showcase/generate", async (
            ShowcaseWorkflowRequest request,
            IRAGQueryService ragService,
            IChatClient chatClient,
            ILogger<Program> logger) =>
        {
            try
            {
                var result = await ShowcaseAgentWorkflow.ExecuteAsync(
                    request,
                    ragService,
                    chatClient,
                    appSettings,
                    logger,
                    CancellationToken.None);

                return Results.Ok(new
                {
                    status = "success",
                    workflow = "Showcase Multi-Stage",
                    result = new
                    {
                        workflowId = result.WorkflowId,
                        fileName = result.FileName,
                        content = result.Content,
                        iterations = result.Iterations,
                        finalScore = result.FinalScore,
                        knowledgeChunksUsed = result.KnowledgeChunksUsed,
                        durationSeconds = result.Duration.TotalSeconds
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Showcase workflow failed");
                return Results.Problem(
                    title: "Workflow execution failed",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("ShowcaseWorkflow")
        .WithDescription("Execute the showcase multi-stage agent generation workflow (7 stages with RAG and critique)");
    }

    private static void MapTestEnhancedRagEndpoint(WebApplication app)
    {
        app.MapPost("/api/test/enhanced-rag", async (
            EnhancedRAGQueryService enhancedRagService,
            ILogger<TestEnhancedRAG> logger) =>
        {
            var tester = new TestEnhancedRAG(enhancedRagService, logger);
            var results = await tester.RunTestsAsync();

            return Results.Ok(new
            {
                status = results.TestsFailed == 0 ? "success" : "partial",
                totalTests = results.TotalTests,
                passed = results.TestsPassed,
                failed = results.TestsFailed,
                successRate = results.TotalTests > 0
                    ? (double)results.TestsPassed / results.TotalTests
                    : 0
            });
        });
    }

    private static void MapEnhancedRAGEndpoint(WebApplication app)
    {
        app.MapPost("/api/enhanced-rag/query", async (
            EnhancedRAGQueryService enhancedRagService,
            EnhancedRAGQueryRequest request) =>
        {
            try
            {
                var options = new Services.RAGQueryOptions
                {
                    EnableQueryOptimization = request.EnableQueryOptimization ?? true,
                    EnableHybridSearch = request.EnableHybridSearch ?? true,
                    ApplySuggestedFilters = request.ApplySuggestedFilters ?? true,
                    RerankByQuality = request.RerankByQuality ?? true,
                    TopK = request.TopK,
                    SimilarityThreshold = request.SimilarityThreshold
                };

                var result = await enhancedRagService.QueryAsync(
                    request.Query,
                    request.ConversationContext,
                    options);

                return Results.Ok(new
                {
                    status = "success",
                    originalQuery = result.OriginalQuery,
                    processedQuery = result.ProcessedQuery,
                    searchMethod = result.SearchMethod,
                    resultCount = result.ResultCount,
                    elapsedMs = result.ElapsedMilliseconds,
                    results = result.Results.Take(10).Select(r => new
                    {
                        chunkId = r.ChunkId,
                        documentType = r.DocumentType,
                        section = r.Section,
                        content = r.Content.Length > 500 ? string.Concat(r.Content.AsSpan(0, 500), "...") : r.Content,
                        similarity = r.Similarity
                    })
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Enhanced RAG query failed",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("EnhancedRAGQuery")
        .WithDescription("Query architecture knowledge with enhanced hybrid search, query optimization, and re-ranking");
    }

    private static void MapKnowledgeSynthesisEndpoint(WebApplication app)
    {
        app.MapPost("/api/knowledge/synthesize", async (
            RAGQueryService ragService,
            KnowledgeSynthesizerService synthesizerService,
            KnowledgeSynthesisRequest request) =>
        {
            try
            {
                // Execute multiple parallel queries
                var queries = new List<string>
                {
                    request.BaseQuery,
                    $"{request.BaseQuery} best practices",
                    $"{request.BaseQuery} code examples",
                    $"{request.BaseQuery} anti-patterns things to avoid",
                    $"{request.BaseQuery} related technologies frameworks"
                };

                var tasks = queries.Select(q => ragService.QueryAsync(q)).ToArray();
                var results = await Task.WhenAll(tasks);

                // Synthesize into knowledge graph
                var synthesisResult = await synthesizerService.SynthesizeAsync(
                    acceptedPatterns: results[1].ToList(),
                    codeExamples: results[2].ToList(),
                    antiPatterns: results[3].ToList(),
                    relatedTech: results[4].ToList(),
                    generalDocs: results[0].ToList());

                return Results.Ok(new
                {
                    status = "success",
                    summary = synthesisResult.Summary,
                    inputChunks = synthesisResult.InputChunkCount,
                    outputItems = synthesisResult.OutputItemCount,
                    duplicatesMerged = synthesisResult.DuplicatesMerged,
                    synthesisTimeMs = synthesisResult.SynthesisTimeMs,
                    knowledgeGraph = new
                    {
                        principles = synthesisResult.KnowledgeGraph.Principles.Count,
                        patterns = synthesisResult.KnowledgeGraph.Patterns.Count,
                        practices = synthesisResult.KnowledgeGraph.Practices.Count,
                        examples = synthesisResult.KnowledgeGraph.Examples.Count,
                        antiPatterns = synthesisResult.KnowledgeGraph.AntiPatterns.Count,
                        relatedTechnologies = synthesisResult.KnowledgeGraph.RelatedTechnologies.Count,
                        totalItems = synthesisResult.KnowledgeGraph.TotalItems
                    },
                    // Include sample items from each category
                    samples = new
                    {
                        principles = synthesisResult.KnowledgeGraph.Principles.Take(2),
                        patterns = synthesisResult.KnowledgeGraph.Patterns.Take(2),
                        examples = synthesisResult.KnowledgeGraph.Examples.Take(2)
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    title: "Knowledge synthesis failed",
                    detail: ex.Message,
                    statusCode: 500);
            }
        })
        .WithName("KnowledgeSynthesis")
        .WithDescription("Synthesize multiple RAG queries into an organized knowledge graph with deduplication");
    }

    private static void MapPgvectorTestEndpoint(WebApplication app, AppSettings appSettings)
    {
        app.MapGet("/api/test/pgvector", async () =>
        {
            try
            {
                var connectionString = appSettings.PostgreSQL.ConnectionString;

                // Create data source with pgvector support
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
                dataSourceBuilder.UseVector();
                await using var dataSource = dataSourceBuilder.Build();

                await using var conn = await dataSource.OpenConnectionAsync();

                // Test pgvector extension
                await using var cmd1 = new NpgsqlCommand("SELECT extversion FROM pg_extension WHERE extname = 'vector'", conn);
                var version = await cmd1.ExecuteScalarAsync();

                // Test vector parameter
                var testVector = new Pgvector.Vector(new float[] { 0.1f, 0.2f, 0.3f });
                await using var cmd2 = new NpgsqlCommand("SELECT @vec::text", conn);
                cmd2.Parameters.AddWithValue("vec", testVector);
                var vectorResult = await cmd2.ExecuteScalarAsync();

                // Test document_chunks query
                await using var cmd3 = new NpgsqlCommand("SELECT COUNT(*) FROM document_chunks WHERE embedding IS NOT NULL", conn);
                var count = await cmd3.ExecuteScalarAsync();

                return Results.Ok(new
                {
                    status = "success",
                    pgvectorVersion = version,
                    vectorTest = vectorResult,
                    chunksWithEmbeddings = count,
                    message = "Pgvector is working correctly"
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "error",
                    message = ex.Message,
                    type = ex.GetType().Name,
                    innerMessage = ex.InnerException?.Message,
                    stackTrace = ex.StackTrace
                }, statusCode: 500);
            }
        }).WithName("TestPgvector");
    }
}
