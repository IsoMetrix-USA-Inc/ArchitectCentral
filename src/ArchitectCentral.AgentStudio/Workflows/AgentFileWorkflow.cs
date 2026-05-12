using ArchitectCentral.AgentStudio.Configuration;
using ArchitectCentral.AgentStudio.Models.Workflow;
using ArchitectCentral.AgentStudio.Workflows.Executors;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Workflows;

/// <summary>
/// Workflow for generating .agent files using the Writer-Critic pattern.
/// 
/// Flow:
/// 1. Writer creates initial .agent file based on discovery
/// 2. Critic reviews and provides feedback with quality score
/// 3. If approved (score >= 0.8): Complete
/// 4. If rejected: Writer revises based on feedback (loops back to step 2)
/// 5. Maximum 3 iterations, then auto-approve
/// </summary>
public static class AgentFileWorkflow
{
    /// <summary>
    /// Build the .agent file generation workflow
    /// </summary>
    public static Workflow Build(IChatClient chatClient, AppSettings settings)
    {
        // Create executors
        var writer = new AgentFileWriterExecutor(chatClient, settings);
        var critic = new AgentFileCriticExecutor(chatClient, settings);

        // Build workflow with conditional routing based on critic's decision
        var workflowBuilder = new WorkflowBuilder(writer)
            // Writer -> Critic
            .AddEdge(writer, critic)
            // Conditional routing from Critic
            .AddSwitch(critic, sw => sw
                // If rejected: loop back to writer for revision
                .AddCase<CriticDecision>(
                    decision => decision?.Approved == false,
                    writer))
            // Output the final approved content from critic
            .WithOutputFrom(critic);

        return workflowBuilder.Build();
    }

    /// <summary>
    /// Execute the workflow and return the generated .agent file
    /// </summary>
    public static async Task<ArtifactDraft> ExecuteAsync(
        DiscoveryState discovery,
        GenerationContext context,
        IChatClient chatClient,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("AGENT FILE GENERATION WORKFLOW");
        Console.WriteLine(new string('=', 80) + "\n");

        var workflow = Build(chatClient, settings);

        // Initialize the shared generation context
        var checkpointManager = CheckpointManager.Default;

        await using var run = await InProcessExecution.RunStreamingAsync(workflow, discovery, checkpointManager, cancellationToken: cancellationToken);

        // Send turn token to trigger the agents (required for agent executors)
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        ArtifactDraft? result = null;

        await foreach (var evt in run.WatchStreamAsync(cancellationToken))
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent agentUpdate:
                    // Stream agent responses to console
                    Console.Write(agentUpdate.Data?.ToString() ?? "");
                    break;

                case WorkflowOutputEvent output:
                    result = output.Data as ArtifactDraft;
                    if (result != null)
                    {
                        Console.WriteLine("\n" + new string('=', 80));
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("[SUCCESS] AGENT FILE GENERATION COMPLETE");
                        Console.ResetColor();
                        Console.WriteLine(new string('=', 80));
                        Console.WriteLine($"\nQuality Score: {result.QualityScore:P0}");
                        Console.WriteLine($"Iterations: {result.Iteration}");
                        Console.WriteLine();
                    }
                    break;

                case SuperStepCompletedEvent superStepCompleted:
                    var checkpoint = superStepCompleted.CompletionInfo?.Checkpoint;
                    if (checkpoint != null)
                    {
                        Console.WriteLine($"[OK] Checkpoint saved");
                    }
                    break;

                case WorkflowErrorEvent workflowError:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine("[ERROR] Workflow Error:");
                    Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "Unknown error");
                    Console.ResetColor();
                    throw new InvalidOperationException(
                        "Agent file workflow failed",
                        workflowError.Exception);

                case ExecutorFailedEvent executorFailed:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"[ERROR] Executor '{executorFailed.ExecutorId}' failed");
                    if (executorFailed.Data != null)
                    {
                        Console.Error.WriteLine($"Error: {executorFailed.Data}");
                    }
                    Console.ResetColor();
                    throw new InvalidOperationException(
                        $"Executor {executorFailed.ExecutorId} failed");
            }
        }

        if (result == null)
        {
            throw new InvalidOperationException("Workflow completed without producing output");
        }

        return result;
    }

    /// <summary>
    /// Visualize the workflow as a Mermaid diagram
    /// </summary>
    public static string ToMermaid(IChatClient chatClient, AppSettings settings)
    {
        var workflow = Build(chatClient, settings);
        return workflow.ToMermaidString();
    }

    /// <summary>
    /// Visualize the workflow as a DOT graph
    /// </summary>
    public static string ToDot(IChatClient chatClient, AppSettings settings)
    {
        var workflow = Build(chatClient, settings);
        return workflow.ToDotString();
    }
}
