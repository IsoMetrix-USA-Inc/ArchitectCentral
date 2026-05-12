using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ArchitectCentral.AgentStudio.Telemetry;

/// <summary>
/// Centralized ActivitySource for distributed tracing across agent workflows
/// </summary>
public static class WorkflowActivitySource
{
    public const string SourceName = "ArchitectCentral.AgentStudio.Workflows";

    private static readonly ActivitySource ActivitySource = new(
        SourceName,
        version: "1.0.0");

    /// <summary>
    /// Start a new activity for a workflow stage
    /// </summary>
    public static Activity? StartStageActivity(
        string stageName,
        string? parentId = null,
        ActivityKind kind = ActivityKind.Internal)
    {
        var activity = ActivitySource.StartActivity(
            $"Workflow.{stageName}",
            kind,
            parentId ?? Activity.Current?.Id ?? string.Empty);

        activity?.SetTag("workflow.stage", stageName);
        activity?.SetTag("workflow.type", "agent.generation");

        return activity;
    }

    /// <summary>
    /// Add metadata to current activity
    /// </summary>
    public static void AddMetadata(this Activity? activity, string key, object? value)
    {
        if (activity == null || value == null) return;
        activity.SetTag(key, value);
    }

    /// <summary>
    /// Record a workflow event
    /// </summary>
    public static void RecordEvent(this Activity? activity, string eventName, Dictionary<string, object?>? tags = null)
    {
        if (activity == null) return;

        var tagsCollection = tags != null
            ? new ActivityTagsCollection(tags)
            : new ActivityTagsCollection();

        activity.AddEvent(new ActivityEvent(eventName, tags: tagsCollection));
    }

    /// <summary>
    /// Record an error in the activity
    /// </summary>
    public static void RecordError(this Activity? activity, Exception ex)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.SetTag("error", true);
        activity.SetTag("error.type", ex.GetType().Name);
        activity.SetTag("error.message", ex.Message);
        activity.SetTag("error.stacktrace", ex.StackTrace);
    }

    /// <summary>
    /// Set the success status of an activity
    /// </summary>
    public static void SetSuccess(this Activity? activity, bool success = true)
    {
        if (activity == null) return;

        activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        activity.SetTag("workflow.success", success);
    }

    /// <summary>
    /// Add RAG query metadata
    /// </summary>
    public static void AddRAGMetadata(this Activity? activity, int resultCount, double avgRelevance)
    {
        if (activity == null) return;

        activity.SetTag("rag.result_count", resultCount);
        activity.SetTag("rag.avg_relevance", avgRelevance);
    }

    /// <summary>
    /// Add synthesis metadata
    /// </summary>
    public static void AddSynthesisMetadata(
        this Activity? activity,
        int inputChunks,
        int outputItems,
        double deduplicationRatio)
    {
        if (activity == null) return;

        activity.SetTag("synthesis.input_chunks", inputChunks);
        activity.SetTag("synthesis.output_items", outputItems);
        activity.SetTag("synthesis.deduplication_ratio", deduplicationRatio);
    }

    /// <summary>
    /// Add critique metadata
    /// </summary>
    public static void AddCritiqueMetadata(this Activity? activity, double qualityScore, bool needsRegeneration)
    {
        if (activity == null) return;

        activity.SetTag("critique.quality_score", qualityScore);
        activity.SetTag("critique.needs_regeneration", needsRegeneration);
    }

    /// <summary>
    /// Add token usage metadata
    /// </summary>
    public static void AddTokenUsage(this Activity? activity, int promptTokens, int completionTokens)
    {
        if (activity == null) return;

        activity.SetTag("llm.prompt_tokens", promptTokens);
        activity.SetTag("llm.completion_tokens", completionTokens);
        activity.SetTag("llm.total_tokens", promptTokens + completionTokens);
    }
}

/// <summary>
/// Custom metrics for agent workflow quality and performance
/// </summary>
public class WorkflowMetrics
{
    private static readonly Meter Meter = new(
        WorkflowActivitySource.SourceName,
        "1.0.0");

    // Counters
    public static readonly Counter<long> WorkflowExecutions = Meter.CreateCounter<long>(
        "workflow.executions",
        description: "Total number of workflow executions");

    public static readonly Counter<long> WorkflowFailures = Meter.CreateCounter<long>(
        "workflow.failures",
        description: "Total number of workflow failures");

    public static readonly Counter<long> RegenerationTriggers = Meter.CreateCounter<long>(
        "workflow.regenerations",
        description: "Number of times regeneration was triggered by critique");

    // Histograms
    public static readonly Histogram<double> WorkflowDuration = Meter.CreateHistogram<double>(
        "workflow.duration",
        unit: "ms",
        description: "Duration of workflow execution");

    public static readonly Histogram<double> RAGRelevanceScore = Meter.CreateHistogram<double>(
        "rag.relevance_score",
        description: "RAG query relevance scores");

    public static readonly Histogram<double> SynthesisDeduplicationRatio = Meter.CreateHistogram<double>(
        "synthesis.deduplication_ratio",
        description: "Ratio of chunks removed during synthesis deduplication");

    public static readonly Histogram<double> CritiqueQualityScore = Meter.CreateHistogram<double>(
        "critique.quality_score",
        description: "Quality scores from critique evaluation");

    public static readonly Histogram<long> TokenUsage = Meter.CreateHistogram<long>(
        "llm.token_usage",
        unit: "tokens",
        description: "Token usage per LLM call");

    // Gauges (using ObservableGauge)
    private static int _activeWorkflows;
    public static readonly ObservableGauge<int> ActiveWorkflows = Meter.CreateObservableGauge(
        "workflow.active",
        () => _activeWorkflows,
        description: "Number of currently active workflows");

    /// <summary>
    /// Record a workflow execution
    /// </summary>
    public static void RecordWorkflowExecution(string stageName, bool success, double durationMs)
    {
        WorkflowExecutions.Add(1,
            new KeyValuePair<string, object?>("stage", stageName),
            new KeyValuePair<string, object?>("success", success));

        if (!success)
        {
            WorkflowFailures.Add(1, new KeyValuePair<string, object?>("stage", stageName));
        }

        WorkflowDuration.Record(durationMs, new KeyValuePair<string, object?>("stage", stageName));
    }

    /// <summary>
    /// Record RAG query metrics
    /// </summary>
    public static void RecordRAGQuery(string queryType, double avgRelevance, int resultCount)
    {
        RAGRelevanceScore.Record(avgRelevance,
            new KeyValuePair<string, object?>("query_type", queryType),
            new KeyValuePair<string, object?>("result_count", resultCount));
    }

    /// <summary>
    /// Record synthesis metrics
    /// </summary>
    public static void RecordSynthesis(int inputChunks, int outputItems)
    {
        var deduplicationRatio = inputChunks > 0
            ? (double)(inputChunks - outputItems) / inputChunks
            : 0.0;

        SynthesisDeduplicationRatio.Record(deduplicationRatio,
            new KeyValuePair<string, object?>("input_chunks", inputChunks),
            new KeyValuePair<string, object?>("output_items", outputItems));
    }

    /// <summary>
    /// Record critique metrics
    /// </summary>
    public static void RecordCritique(double qualityScore, bool needsRegeneration)
    {
        CritiqueQualityScore.Record(qualityScore,
            new KeyValuePair<string, object?>("needs_regeneration", needsRegeneration));

        if (needsRegeneration)
        {
            RegenerationTriggers.Add(1);
        }
    }

    /// <summary>
    /// Record LLM token usage
    /// </summary>
    public static void RecordTokenUsage(string operation, int promptTokens, int completionTokens)
    {
        var total = promptTokens + completionTokens;
        TokenUsage.Record(total,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("type", "total"));

        TokenUsage.Record(promptTokens,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("type", "prompt"));

        TokenUsage.Record(completionTokens,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("type", "completion"));
    }

    /// <summary>
    /// Increment active workflow counter
    /// </summary>
    public static void IncrementActiveWorkflows()
    {
        Interlocked.Increment(ref _activeWorkflows);
    }

    /// <summary>
    /// Decrement active workflow counter
    /// </summary>
    public static void DecrementActiveWorkflows()
    {
        Interlocked.Decrement(ref _activeWorkflows);
    }
}
