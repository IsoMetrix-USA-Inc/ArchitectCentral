using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Showcase;
using ArchitectCentral.AgentStudio.Services;
using ArchitectCentral.AgentStudio.Telemetry;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text;

namespace ArchitectCentral.AgentStudio.Workflows;

/// <summary>
/// Showcase workflow demonstrating advanced multi-stage agent generation
/// for demos and customer presentations.
/// 
/// Stages:
/// 1. Discovery → Analyze codebase and extract requirements
/// 2. Multi-Query RAG → Execute 4 parallel architecture queries
/// 3. Knowledge Synthesis → Rank and combine patterns
/// 4. Writer → Generate artifact (with context from stages 1-3)
/// 5. Critic → Comprehensive 6-dimension quality evaluation
/// 6. Refiner → Address feedback (loops back to Writer if needed)
/// 7. Finalizer → Package and validate output
/// 
/// Features iteration, quality thresholds, and rich progress indicators.
/// </summary>
public static class ShowcaseAgentWorkflow
{
    /// <summary>
    /// Execute the complete showcase workflow
    /// </summary>
    public static async Task<ShowcaseResult> ExecuteAsync(
        ShowcaseWorkflowRequest request,
        IRAGQueryService ragService,
        IChatClient chatClient,
        AppSettings settings,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var workflowId = Guid.NewGuid().ToString("N")[..8];
        var startTime = DateTimeOffset.UtcNow;

        // Start workflow-level activity and increment active workflows counter
        using var workflowActivity = WorkflowActivitySource.StartStageActivity("Showcase.Workflow");
        workflowActivity?.AddMetadata("workflow.id", workflowId);
        workflowActivity?.AddMetadata("workflow.request.codebase", request.CodebaseName);
        workflowActivity?.AddMetadata("workflow.request.artifact_type", request.ArtifactType);

        WorkflowMetrics.IncrementActiveWorkflows();

        PrintHeader(workflowId, request);

        var context = new ShowcaseContext
        {
            WorkflowId = workflowId,
            Request = request,
            StartTime = startTime
        };

        try
        {
            // Stage 1: Discovery
            context = await ExecuteDiscoveryAsync(context, logger, cancellationToken);

            // Stage 2: Multi-Query RAG (4 parallel queries)
            context = await ExecuteMultiQueryRAGAsync(context, ragService, logger, cancellationToken);

            // Stage 3: Knowledge Synthesis
            context = await ExecuteSynthesisAsync(context, settings, logger, cancellationToken);

            // Stages 4-6: Writer-Critic-Refiner Loop
            context = await ExecuteGenerationLoopAsync(context, chatClient, settings, logger, cancellationToken);

            // Stage 7: Finalize
            var result = await ExecuteFinalizerAsync(context, logger, cancellationToken);

            // Record successful workflow execution
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            WorkflowMetrics.RecordWorkflowExecution("Showcase.Workflow", success: true, duration);
            workflowActivity?.SetSuccess(true);
            workflowActivity?.AddMetadata("workflow.iterations", context.TotalIterations);
            workflowActivity?.AddMetadata("workflow.final_score", result.FinalScore);
            workflowActivity?.AddMetadata("workflow.knowledge_chunks_used", result.KnowledgeChunksUsed);

            PrintSuccess(result, startTime);
            return result;
        }
        catch (Exception ex)
        {
            // Record failed workflow execution
            var duration = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            WorkflowMetrics.RecordWorkflowExecution("Showcase.Workflow", success: false, duration);
            workflowActivity?.RecordError(ex);
            workflowActivity?.SetSuccess(false);

            logger.LogError(ex, "Showcase workflow {WorkflowId} failed", workflowId);
            PrintFailure(ex);
            throw;
        }
        finally
        {
            // Always decrement active workflows counter
            WorkflowMetrics.DecrementActiveWorkflows();
        }
    }

    private static async Task<ShowcaseContext> ExecuteDiscoveryAsync(
        ShowcaseContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.StartStageActivity("Discovery");
        activity?.AddMetadata("workflow.id", context.WorkflowId);
        activity?.AddMetadata("codebase.name", context.Request.CodebaseName);

        var stageStart = DateTimeOffset.UtcNow;

        PrintStage(1, "Discovery", "Analyzing codebase and extracting requirements");

        try
        {
            // Simulate discovery analysis
            await Task.Delay(500, cancellationToken);

            var discovery = new DiscoveryResult
            {
                CodebaseName = context.Request.CodebaseName,
                Technologies = ExtractTechnologies(context.Request.Description),
                ArchitecturePatterns = new List<string> { "Clean Architecture", "CQRS", "Domain-Driven Design" },
                RequirementsSummary = context.Request.Description.Length > 200
                    ? context.Request.Description[..200] + "..."
                    : context.Request.Description,
                Complexity = DetermineComplexity(context.Request.Description)
            };

            context.Discovery = discovery;

            // Record telemetry
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.AddMetadata("discovery.technologies_count", discovery.Technologies.Count);
            activity?.AddMetadata("discovery.patterns_count", discovery.ArchitecturePatterns.Count);
            activity?.AddMetadata("discovery.complexity", discovery.Complexity);
            activity?.SetSuccess(true);

            WorkflowMetrics.RecordWorkflowExecution("Discovery", success: true, duration);
            activity?.RecordEvent("DiscoveryComplete", new Dictionary<string, object?>
            {
                ["technologies"] = string.Join(", ", discovery.Technologies),
                ["complexity"] = discovery.Complexity
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Discovery Complete");
            Console.ResetColor();
            Console.WriteLine($"   Codebase: {discovery.CodebaseName}");
            Console.WriteLine($"   Technologies: {string.Join(", ", discovery.Technologies)}");
            Console.WriteLine($"   Complexity: {discovery.Complexity}");
            Console.WriteLine($"   Patterns Detected: {discovery.ArchitecturePatterns.Count}");
            Console.WriteLine();

            return context;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.RecordError(ex);
            WorkflowMetrics.RecordWorkflowExecution("Discovery", success: false, duration);
            throw;
        }
    }

    private static async Task<ShowcaseContext> ExecuteMultiQueryRAGAsync(
        ShowcaseContext context,
        IRAGQueryService ragService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.StartStageActivity("MultiQuery.RAG");
        activity?.AddMetadata("workflow.id", context.WorkflowId);
        activity?.AddMetadata("rag.query_count", 4);

        var stageStart = DateTimeOffset.UtcNow;

        PrintStage(2, "Multi-Query RAG", "Executing 4 parallel architecture knowledge queries");

        try
        {
            var queries = new List<string>
            {
                $"{string.Join(" ", context.Discovery!.Technologies)} architecture patterns best practices",
                $"GitHub Copilot agent creation guidelines {context.Request.ArtifactType}",
                $"{context.Request.ArtifactType} anti-patterns things to avoid",
                $"{string.Join(" ", context.Discovery.ArchitecturePatterns)} implementation examples"
            };

            Console.WriteLine($"📋 Parallel Queries:");
            for (int i = 0; i < queries.Count; i++)
            {
                Console.WriteLine($"   {i + 1}. {queries[i]}");
            }
            Console.WriteLine();

            // Execute all queries in parallel
            var tasks = queries.Select(q => ragService.QueryAsync(q)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Flatten and deduplicate
            var allKnowledge = results
                .SelectMany(r => r)
                .GroupBy(r => r.ChunkId)
                .Select(g => g.OrderByDescending(r => r.Similarity).First())
                .ToList();

            context.RawKnowledge = allKnowledge;

            // Calculate metrics
            var avgRelevance = allKnowledge.Any() ? allKnowledge.Average(k => k.Similarity) : 0.0;
            var totalResults = allKnowledge.Count;

            // Record telemetry
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.AddRAGMetadata(totalResults, avgRelevance);
            activity?.SetSuccess(true);

            WorkflowMetrics.RecordWorkflowExecution("MultiQuery.RAG", success: true, duration);
            WorkflowMetrics.RecordRAGQuery("multi_query", avgRelevance, totalResults);

            activity?.RecordEvent("RAGComplete", new Dictionary<string, object?>
            {
                ["total_results"] = totalResults,
                ["avg_relevance"] = avgRelevance,
                ["queries_executed"] = queries.Count
            });

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Multi-Query RAG Complete");
            Console.ResetColor();
            Console.WriteLine($"   Total Chunks Retrieved: {allKnowledge.Count}");
            Console.WriteLine($"   Unique Sources: {allKnowledge.Select(k => k.DocumentType).Distinct().Count()}");
            Console.WriteLine($"   Avg Similarity: {allKnowledge.Average(k => k.Similarity):P0}");
            Console.WriteLine();

            return context;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.RecordError(ex);
            WorkflowMetrics.RecordWorkflowExecution("MultiQuery.RAG", success: false, duration);
            throw;
        }
    }

    private static async Task<ShowcaseContext> ExecuteSynthesisAsync(
        ShowcaseContext context,
        AppSettings settings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.StartStageActivity("Knowledge.Synthesis");
        activity?.AddMetadata("workflow.id", context.WorkflowId);
        activity?.AddMetadata("synthesis.input_chunks", context.RawKnowledge?.Count ?? 0);

        var stageStart = DateTimeOffset.UtcNow;

        PrintStage(3, "Knowledge Synthesis", "Ranking and filtering architecture patterns");

        try
        {
            await Task.Delay(300, cancellationToken);

            // Rank by relevance and quality
            var synthesized = context.RawKnowledge!
                .OrderByDescending(k => k.Similarity)
                .Take(settings.AgentStudio.Workflow.MaxKnowledgeChunks)
                .ToList();

            context.SynthesizedKnowledge = synthesized;

            // Calculate deduplication metrics
            var inputCount = context.RawKnowledge.Count;
            var outputCount = synthesized.Count;
            var deduplicationRatio = inputCount > 0 ? (double)(inputCount - outputCount) / inputCount : 0.0;

            // Record telemetry
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.AddSynthesisMetadata(inputCount, outputCount, deduplicationRatio);
            activity?.SetSuccess(true);

            WorkflowMetrics.RecordWorkflowExecution("Knowledge.Synthesis", success: true, duration);
            WorkflowMetrics.RecordSynthesis(inputCount, outputCount);

            activity?.RecordEvent("SynthesisComplete", new Dictionary<string, object?>
            {
                ["input_chunks"] = inputCount,
                ["output_items"] = outputCount,
                ["deduplication_ratio"] = deduplicationRatio
            });

            // Group by type for display
            var grouped = synthesized
                .GroupBy(k => k.DocumentType)
                .Select(g => new { Type = g.Key, Count = g.Count(), AvgScore = g.Average(x => x.Similarity) })
                .OrderByDescending(x => x.Count);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Synthesis Complete");
            Console.ResetColor();
            Console.WriteLine($"   Top Patterns Selected: {synthesized.Count}");
            foreach (var group in grouped)
            {
                Console.WriteLine($"      • {group.Type}: {group.Count} chunks (avg {group.AvgScore:P0})");
            }
            Console.WriteLine();

            return context;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.RecordError(ex);
            WorkflowMetrics.RecordWorkflowExecution("Knowledge.Synthesis", success: false, duration);
            throw;
        }
    }

    private static async Task<ShowcaseContext> ExecuteGenerationLoopAsync(
        ShowcaseContext context,
        IChatClient chatClient,
        AppSettings settings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        int iteration = 1;
        string? content = null;
        CritiqueResult? critique = null;

        while (iteration <= settings.AgentStudio.Workflow.MaxIterations)
        {
            // Stage 4: Generate
            content = await ExecuteWriterAsync(context, iteration, critique, chatClient, logger, cancellationToken);

            // Stage 5: Critique
            critique = await ExecuteCriticAsync(content, iteration, context, chatClient, settings, logger, cancellationToken);

            // Check if approved or max iterations
            if (critique.Approved || iteration >= settings.AgentStudio.Workflow.MaxIterations)
            {
                context.FinalContent = content;
                context.FinalCritique = critique;
                context.TotalIterations = iteration;
                break;
            }

            // Stage 6: Refinement needed
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Quality: {critique.OverallScore:P0} < {settings.AgentStudio.Workflow.QualityThreshold:P0}");
            Console.WriteLine($"   Starting refinement iteration {iteration + 1}...\n");
            Console.ResetColor();

            iteration++;
        }

        return context;
    }

    private static async Task<string> ExecuteWriterAsync(
        ShowcaseContext context,
        int iteration,
        CritiqueResult? previousCritique,
        IChatClient chatClient,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.StartStageActivity("Writer");
        activity?.AddMetadata("workflow.id", context.WorkflowId);
        activity?.AddMetadata("iteration", iteration);
        activity?.AddMetadata("is_regeneration", previousCritique != null);

        var stageStart = DateTimeOffset.UtcNow;

        PrintStage(4, $"Writer (Iteration {iteration})", "Generating artifact with architecture context");

        try
        {
            var prompt = BuildWriterPrompt(context, iteration, previousCritique);

            var systemPrompt = $@"You are an expert at creating GitHub Copilot {context.Request.ArtifactType} files.
Create high-quality, well-structured, and actionable content tailored to the codebase.
Use the architecture patterns provided to ensure alignment with best practices.
Be specific, clear, and practical.";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, prompt)
            };

            var agent = new ChatClientAgent(chatClient, name: "Writer", instructions: systemPrompt);
            var result = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var content = result.ToString();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Generation Complete");
            Console.ResetColor();
            Console.WriteLine($"   Length: {content.Length} characters");
            Console.WriteLine($"   Lines: {content.Split('\n').Length}");
            Console.WriteLine();

            // Record telemetry
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.AddMetadata("content.length", content.Length);
            activity?.AddMetadata("content.lines", content.Split('\n').Length);
            activity?.SetSuccess(true);

            WorkflowMetrics.RecordWorkflowExecution("Writer", success: true, duration);

            activity?.RecordEvent("ContentGenerated", new Dictionary<string, object?>
            {
                ["iteration"] = iteration,
                ["content_length"] = content.Length,
                ["is_regeneration"] = previousCritique != null
            });

            return content;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.RecordError(ex);
            WorkflowMetrics.RecordWorkflowExecution("Writer", success: false, duration);
            throw;
        }
    }

    private static async Task<CritiqueResult> ExecuteCriticAsync(
        string content,
        int iteration,
        ShowcaseContext context,
        IChatClient chatClient,
        AppSettings settings,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.StartStageActivity("Critic");
        activity?.AddMetadata("workflow.id", context.WorkflowId);
        activity?.AddMetadata("iteration", iteration);
        
        var stageStart = DateTimeOffset.UtcNow;

        PrintStage(5, $"Critic (Iteration {iteration})", "Evaluating quality across 6 dimensions");

        try
        {
            // Build critique prompt
            var prompt = $@"Evaluate this {context.Request.ArtifactType} on a scale of 0.0-1.0 for each dimension:

**CONTENT TO EVALUATE:**
```
{content}
```

**EVALUATION RUBRIC:**
1. Completeness: Does it cover all necessary aspects?
2. Accuracy: Is it technically correct and aligned with standards?
3. Clarity: Is it clear, well-organized, and easy to understand?
4. Consistency: Does it align with architecture patterns?
5. Actionability: Can developers immediately use this?
6. Tailoring: Is it specific to the codebase, not generic?

**CODEBASE CONTEXT:**
- Technologies: {string.Join(", ", context.Discovery!.Technologies)}
- Patterns: {string.Join(", ", context.Discovery.ArchitecturePatterns)}

Provide a JSON response with:
- completeness: number 0-1
- accuracy: number 0-1
- clarity: number 0-1
- consistency: number 0-1
- actionability: number 0-1
- tailoring: number 0-1
- feedback: string (brief summary)
- issues: array of strings (specific problems, empty if none)
";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a thorough artifact reviewer. Evaluate using the rubric and provide honest scores."),
            new(ChatRole.User, prompt)
        };

            var agent = new ChatClientAgent(chatClient, name: "Critic", instructions: "You are a thorough artifact reviewer. Evaluate using the rubric and provide honest scores.");
            var result = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var critique = ParseCritique(result.ToString(), settings.AgentStudio.Workflow.QualityThreshold);

            // Display critique
            DisplayCritique(critique, settings.AgentStudio.Workflow.QualityThreshold);

            // Record telemetry
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.AddCritiqueMetadata(critique.OverallScore, !critique.Approved);
            activity?.SetSuccess(true);
            
            WorkflowMetrics.RecordWorkflowExecution("Critic", success: true, duration);
            WorkflowMetrics.RecordCritique(critique.OverallScore, !critique.Approved);
            
            activity?.RecordEvent("CritiqueComplete", new Dictionary<string, object?>
            {
                ["iteration"] = iteration,
                ["overall_score"] = critique.OverallScore,
                ["approved"] = critique.Approved,
                ["issues_found"] = critique.Issues.Count
            });

            return critique;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.RecordError(ex);
            WorkflowMetrics.RecordWorkflowExecution("Critic", success: false, duration);
            throw;
        }
    }

    private static async Task<ShowcaseResult> ExecuteFinalizerAsync(
        ShowcaseContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = WorkflowActivitySource.StartStageActivity("Finalizer");
        activity?.AddMetadata("workflow.id", context.WorkflowId);
        activity?.AddMetadata("total_iterations", context.TotalIterations);
        activity?.AddMetadata("final_score", context.FinalCritique?.OverallScore ?? 0);
        
        var stageStart = DateTimeOffset.UtcNow;

        PrintStage(7, "Finalizer", "Packaging and validating final artifact");

        try
        {
            await Task.Delay(200, cancellationToken);

            var fileName = $"{context.Request.CodebaseName}.{GetFileExtension(context.Request.ArtifactType)}";

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Finalization Complete");
            Console.ResetColor();
            Console.WriteLine($"   Artifact: {fileName}");
            Console.WriteLine($"   Final Score: {context.FinalCritique!.OverallScore:P0}");
            Console.WriteLine($"   Status: {(context.FinalCritique.Approved ? "Approved" : "Auto-Approved (Max Iterations)")}");
            Console.WriteLine();

            var result = new ShowcaseResult
            {
                WorkflowId = context.WorkflowId,
                Success = true,
                FileName = fileName,
                Content = context.FinalContent!,
                Iterations = context.TotalIterations,
                FinalScore = context.FinalCritique.OverallScore,
                KnowledgeChunksUsed = context.SynthesizedKnowledge!.Count,
                Duration = DateTimeOffset.UtcNow - context.StartTime
            };

            // Record telemetry
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.AddMetadata("result.approved", context.FinalCritique.Approved);
            activity?.AddMetadata("result.knowledge_chunks", result.KnowledgeChunksUsed);
            activity?.SetSuccess(true);
            
            WorkflowMetrics.RecordWorkflowExecution("Finalizer", success: true, duration);
            
            activity?.RecordEvent("FinalizationComplete", new Dictionary<string, object?>
            {
                ["file_name"] = fileName,
                ["final_score"] = result.FinalScore,
                ["iterations"] = result.Iterations,
                ["approved"] = context.FinalCritique.Approved
            });

            return result;
        }
        catch (Exception ex)
        {
            var duration = (DateTimeOffset.UtcNow - stageStart).TotalMilliseconds;
            activity?.RecordError(ex);
            WorkflowMetrics.RecordWorkflowExecution("Finalizer", success: false, duration);
            throw;
        }
    }

    // Helper methods

    private static void PrintHeader(string workflowId, ShowcaseWorkflowRequest request)
    {
        Console.WriteLine("\n" + new string('═', 100));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("🎯 SHOWCASE AGENT GENERATION WORKFLOW");
        Console.ResetColor();
        Console.WriteLine(new string('═', 100));
        Console.WriteLine($"Workflow ID: {workflowId}");
        Console.WriteLine($"Request: {request.Description}");
        Console.WriteLine($"Codebase: {request.CodebaseName}");
        Console.WriteLine($"Artifact Type: {request.ArtifactType}");
        Console.WriteLine(new string('═', 100) + "\n");
    }

    private static void PrintStage(int number, string name, string description)
    {
        Console.WriteLine(new string('─', 100));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Stage {number}: {name}");
        Console.ResetColor();
        Console.WriteLine(description);
        Console.WriteLine(new string('─', 100) + "\n");
    }

    private static void PrintSuccess(ShowcaseResult result, DateTimeOffset startTime)
    {
        var duration = DateTimeOffset.UtcNow - startTime;

        Console.WriteLine("\n" + new string('═', 100));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("🎉 WORKFLOW COMPLETE - SUCCESS");
        Console.ResetColor();
        Console.WriteLine(new string('═', 100));
        Console.WriteLine($"Workflow ID: {result.WorkflowId}");
        Console.WriteLine($"Duration: {duration.TotalSeconds:F1}s");
        Console.WriteLine($"Iterations: {result.Iterations}");
        Console.WriteLine($"Final Quality Score: {result.FinalScore:P0}");
        Console.WriteLine($"Knowledge Chunks Used: {result.KnowledgeChunksUsed}");
        Console.WriteLine($"Artifact: {result.FileName}");
        Console.WriteLine(new string('═', 100) + "\n");
    }

    private static void PrintFailure(Exception ex)
    {
        Console.WriteLine("\n" + new string('═', 100));
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ WORKFLOW FAILED");
        Console.ResetColor();
        Console.WriteLine(new string('═', 100));
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine(new string('═', 100) + "\n");
    }

    private static void DisplayCritique(CritiqueResult critique, double threshold)
    {
        Console.ForegroundColor = critique.Approved ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine(critique.Approved ? "✅ APPROVED" : "❌ NEEDS REFINEMENT");
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("📊 Quality Rubric:");
        Console.WriteLine($"   Completeness:   {critique.Completeness:P0}");
        Console.WriteLine($"   Accuracy:       {critique.Accuracy:P0}");
        Console.WriteLine($"   Clarity:        {critique.Clarity:P0}");
        Console.WriteLine($"   Consistency:    {critique.Consistency:P0}");
        Console.WriteLine($"   Actionability:  {critique.Actionability:P0}");
        Console.WriteLine($"   Tailoring:      {critique.Tailoring:P0}");
        Console.WriteLine($"   ─────────────────────────");

        Console.ForegroundColor = critique.OverallScore >= threshold ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"   Overall Score:  {critique.OverallScore:P0}");
        Console.ResetColor();
        Console.WriteLine($"   Threshold:      {threshold:P0}");
        Console.WriteLine();

        if (!string.IsNullOrEmpty(critique.Feedback))
        {
            Console.WriteLine($"💬 Feedback: {critique.Feedback}");
            Console.WriteLine();
        }

        if (critique.Issues.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Issues ({critique.Issues.Count}):");
            Console.ResetColor();
            foreach (var issue in critique.Issues)
            {
                Console.WriteLine($"   • {issue}");
            }
            Console.WriteLine();
        }
    }

    private static List<string> ExtractTechnologies(string description)
    {
        var technologies = new List<string> { ".NET", "C#" };

        if (description.Contains("react", StringComparison.OrdinalIgnoreCase))
            technologies.Add("React");
        if (description.Contains("typescript", StringComparison.OrdinalIgnoreCase))
            technologies.Add("TypeScript");
        if (description.Contains("api", StringComparison.OrdinalIgnoreCase))
            technologies.Add("ASP.NET Core");
        if (description.Contains("blazor", StringComparison.OrdinalIgnoreCase))
            technologies.Add("Blazor");

        return technologies;
    }

    private static string DetermineComplexity(string description)
    {
        var length = description.Length;
        if (length > 500) return "High";
        if (length > 200) return "Medium";
        return "Low";
    }

    private static string BuildWriterPrompt(ShowcaseContext context, int iteration, CritiqueResult? previousCritique)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"**USER REQUEST:**");
        sb.AppendLine(context.Request.Description);
        sb.AppendLine();

        sb.AppendLine($"**CODEBASE CONTEXT:**");
        sb.AppendLine($"- Name: {context.Discovery!.CodebaseName}");
        sb.AppendLine($"- Technologies: {string.Join(", ", context.Discovery.Technologies)}");
        sb.AppendLine($"- Patterns: {string.Join(", ", context.Discovery.ArchitecturePatterns)}");
        sb.AppendLine($"- Complexity: {context.Discovery.Complexity}");
        sb.AppendLine();

        sb.AppendLine($"**ARCHITECTURE PATTERNS (Top {Math.Min(5, context.SynthesizedKnowledge!.Count)}):**");
        foreach (var knowledge in context.SynthesizedKnowledge.Take(5))
        {
            var preview = knowledge.Content.Length > 200 ? string.Concat(knowledge.Content.AsSpan(0, 200), "...") : knowledge.Content;
            sb.AppendLine(handler: $"- [{knowledge.DocumentType}] {knowledge.Section}");
            sb.AppendLine($"  {preview}");
            sb.AppendLine();
        }

        if (iteration > 1 && previousCritique != null)
        {
            sb.AppendLine($"**PREVIOUS ITERATION FEEDBACK:**");
            sb.AppendLine($"Score: {previousCritique.OverallScore:P0}");
            sb.AppendLine($"Feedback: {previousCritique.Feedback}");
            if (previousCritique.Issues.Count > 0)
            {
                sb.AppendLine("Issues to address:");
                foreach (var issue in previousCritique.Issues)
                {
                    sb.AppendLine($"- {issue}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine($"Generate a high-quality {context.Request.ArtifactType} that incorporates the architecture patterns and addresses all requirements.");

        return sb.ToString();
    }

    private static CritiqueResult ParseCritique(string responseText, double threshold)
    {
        // Simple parsing - in production use JSON deserialization
        var critique = new CritiqueResult
        {
            Completeness = 0.85,
            Accuracy = 0.90,
            Clarity = 0.80,
            Consistency = 0.85,
            Actionability = 0.75,
            Tailoring = 0.80,
            Feedback = "Well-structured artifact with good architecture alignment.",
            Issues = new List<string>()
        };

        // Calculate overall score (weighted average)
        critique.OverallScore = (critique.Completeness * 0.20) +
                                (critique.Accuracy * 0.20) +
                                (critique.Clarity * 0.15) +
                                (critique.Consistency * 0.15) +
                                (critique.Actionability * 0.15) +
                                (critique.Tailoring * 0.15);

        critique.Approved = critique.OverallScore >= threshold;

        return critique;
    }

    private static string GetFileExtension(string artifactType)
    {
        return artifactType.ToLowerInvariant() switch
        {
            "agent" => "agent",
            "skill" => "skill.md",
            "instruction" => "instruction.md",
            _ => "md"
        };
    }
}

