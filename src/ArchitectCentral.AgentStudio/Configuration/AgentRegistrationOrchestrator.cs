using ArchitectCentral.AgentStudio.Services;
using ArchitectCentral.AgentStudio.Workflows;
using ArchitectCentral.AgentStudio.Workflows.Executors;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace ArchitectCentral.AgentStudio.Configuration;

/// <summary>
/// Orchestrates registration of all AI agents with the Agent Framework
/// Keeps Program.cs clean by extracting agent registration logic
/// </summary>
public static class AgentRegistrationOrchestrator
{
    /// <summary>
    /// Register all agents with the builder
    /// </summary>
    public static void RegisterAgents(WebApplicationBuilder builder)
    {
        // Get tool instances from builder's service collection for agent registration
        using (var tempServiceProvider = builder.Services.BuildServiceProvider())
        {
            var programTools = tempServiceProvider.GetRequiredService<ProgramAgentTools>();
            var agentTools = tempServiceProvider.GetRequiredService<AgentGenerationTools>();

            // Register core agents
            RegisterArchitectAssistant(builder, programTools);
            RegisterAgentGenerator(builder, programTools, agentTools);
            RegisterRagDebugger(builder, programTools);

            // Register workflow agents
            RegisterWorkflowAgents(builder, programTools);
            RegisterAgentGenerationWorkflow(builder);
            RegisterShowcaseWorkflow(builder);
        }
    }

    private static void RegisterArchitectAssistant(WebApplicationBuilder builder, ProgramAgentTools programTools)
    {
        builder.AddAIAgent(
            "architect-assistant",
            EnhancedSystemPrompts.ArchitectAssistant)
            .WithAITools(programTools.GetAgentTools());
    }

    private static void RegisterAgentGenerator(WebApplicationBuilder builder, ProgramAgentTools programTools, AgentGenerationTools agentTools)
    {
        builder.AddAIAgent(
            "agent-generator",
            EnhancedSystemPrompts.AgentGenerator)
            .WithAITools(
                AIFunctionFactory.Create(programTools.QueryArchitectureDocs, name: "query_architecture_docs"),
                agentTools.ReadExistingArtifact,
                agentTools.ListRepositoryFiles,
                agentTools.AnalyzeCodebase
            );
    }

    private static void RegisterRagDebugger(WebApplicationBuilder builder, ProgramAgentTools programTools)
    {
        builder.AddAIAgent(
            "rag-debugger",
            EnhancedSystemPrompts.RagDebugger)
            .WithAITool(AIFunctionFactory.Create(programTools.QueryArchitectureDocs, name: "query_architecture_docs"));
    }

    private static void RegisterWorkflowAgents(WebApplicationBuilder builder, ProgramAgentTools programTools)
    {
        // Define Writer agent for workflow (get tools from service provider)
        using (var tempServiceProvider2 = builder.Services.BuildServiceProvider())
        {
            var programTools2 = tempServiceProvider2.GetRequiredService<ProgramAgentTools>();
            builder.AddAIAgent(
                "agent-writer",
                """
                You are the Agent File Writer. Generate comprehensive .agent configuration files for GitHub Copilot.
                
                When you receive a request, query architecture patterns using query_architecture_docs tool,
                then generate a complete .agent file in markdown format with frontmatter (---), name, description,
                instructions, skills, and conversation_starters.
                """)
                .WithAITools(
                    AIFunctionFactory.Create(programTools2.QueryArchitectureDocs, name: "query_architecture_docs")
                );
        }

        // Define Critic agent for workflow
        builder.AddAIAgent(
            "agent-critic",
            """
            You are the Agent File Critic. Review .agent files for quality.
            
            Evaluate on: Completeness (30%), Clarity (25%), Standards (25%), Practical Value (20%)
            
            Respond in format:
            SCORE: [0.0-1.0]
            APPROVED: [YES if >= 0.8, NO if < 0.8]
            FEEDBACK: [specific improvement suggestions]
            """);
    }

    private static readonly string[] sourceArray = new[] { "agent-writer", "agent-critic" };

    private static void RegisterAgentGenerationWorkflow(WebApplicationBuilder builder)
    {
        // Register the workflow - creates visual graph in DevUI with Writer -> Critic
        builder.AddWorkflow("agent-generation-workflow", (sp, key) =>
        {
            // Get the actual agent instances by name
            var agents = sourceArray.Select(name => sp.GetRequiredKeyedService<AIAgent>(name));

            // Build a sequential workflow: Writer first, then Critic
            return AgentWorkflowBuilder.BuildSequential(workflowName: key, agents: agents);

        }).AddAsAIAgent(); // Makes workflow visible in DevUI
    }

    private static void RegisterShowcaseWorkflow(WebApplicationBuilder builder)
    {
        try
        {
            Console.WriteLine("\n[DEBUG] Starting showcase workflow registration...");

            // Register the showcase workflow using the workflow builder pattern
            builder.AddWorkflow("showcase-workflow", (sp, key) =>
            {
                try
                {
                    Console.WriteLine("[DEBUG] Workflow factory invoked");

                    // Get dependencies from service provider
                    var chatClient = sp.GetRequiredService<IChatClient>();
                    var ragService = sp.GetRequiredService<IRAGQueryService>();
                    var settings = sp.GetRequiredService<AppSettings>();

                    Console.WriteLine("[DEBUG] Dependencies resolved");

                    // Create executors for each stage (including new input executor)
                    var inputExecutor = new ShowcaseInputExecutor();  // NEW: Accepts ChatMessage from DevUI
                    var discoveryExecutor = new ShowcaseDiscoveryExecutor(chatClient, ragService, settings);
                    var multiQueryExecutor = new ShowcaseMultiQueryExecutor(ragService, settings);
                    var synthesisExecutor = new ShowcaseSynthesisExecutor(chatClient, settings);
                    var writerExecutor = new ShowcaseWriterExecutor(chatClient, settings);
                    var criticExecutor = new ShowcaseCriticExecutor(chatClient, settings);
                    var refinerExecutor = new ShowcaseRefinerExecutor(chatClient, settings);
                    var finalizerExecutor = new ShowcaseFinalizerExecutor(settings);

                    Console.WriteLine("[DEBUG] All executors created");

                    // Build the workflow with conditional routing - START WITH INPUT EXECUTOR
                    var workflowBuilder = new WorkflowBuilder(inputExecutor)
                        .WithName(key);  // Set the workflow name

                    Console.WriteLine("[DEBUG] WorkflowBuilder created with name");

                    workflowBuilder
                        // Stage 0 (Input) -> Stage 1 (Discovery)
                        .AddEdge(inputExecutor, discoveryExecutor)
                        // Stage 1 (Discovery) -> Stage 2 (Multi-Query RAG)
                        .AddEdge(discoveryExecutor, multiQueryExecutor)
                        // Stage 2 (Multi-Query) -> Stage 3 (Synthesis)
                        .AddEdge(multiQueryExecutor, synthesisExecutor)
                        // Stage 3 (Synthesis) -> Stage 4 (Writer)
                        .AddEdge(synthesisExecutor, writerExecutor)
                        // Stage 4 (Writer) -> Stage 5 (Critic)
                        .AddEdge(writerExecutor, criticExecutor)
                        // Conditional routing from Critic
                        .AddSwitch(criticExecutor, sw => sw
                            // If rejected: go to Refiner (Stage 6)
                            .AddCase<ShowcaseCriticDecision>(
                                decision => decision?.Approved == false,
                                refinerExecutor))
                        // Stage 6 (Refiner) -> Back to Writer for revision
                        .AddEdge(refinerExecutor, writerExecutor)
                        // When approved: go to Finalizer (Stage 7)
                        .AddSwitch(criticExecutor, sw => sw
                            .AddCase<ShowcaseCriticDecision>(
                                decision => decision?.Approved == true,
                                finalizerExecutor))
                        // Output the final result from finalizer
                        .WithOutputFrom(finalizerExecutor);

                    Console.WriteLine("[DEBUG] All edges added, building workflow...");

                    var workflow = workflowBuilder.Build();

                    Console.WriteLine("[DEBUG] Workflow built successfully!");
                    return workflow;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Workflow factory exception: {ex.GetType().Name}: {ex.Message}");
                    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                    throw;
                }

            }).AddAsAIAgent(); // Makes workflow visible in DevUI with visual graph

            Console.WriteLine("[DEBUG] Showcase workflow registered successfully!\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Failed to register showcase workflow: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[FATAL] Stack trace: {ex.StackTrace}");
            // Re-throw to see if Aspire shows it
            throw;
        }
    }
}
