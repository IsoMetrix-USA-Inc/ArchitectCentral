namespace ArchitectCentral.AgentStudio.UI;

/// <summary>
/// Handles all console output for the application startup and web mode
/// Keeps Program.cs clean by extracting UI concerns
/// </summary>
public static class StartupUI
{
    /// <summary>
    /// Display OTEL configuration information
    /// </summary>
    public static void ShowOTELConfiguration(string otlpEndpoint)
    {
        Console.WriteLine($"[INFO] OpenTelemetry configured - OTLP endpoint: {otlpEndpoint}");
        Console.WriteLine("[TIP] To view traces, run Aspire Dashboard: docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest");
    }

    /// <summary>
    /// Display workflow creation message
    /// </summary>
    public static void ShowWorkflowCreation()
    {
        Console.WriteLine("\n[BUILD] Creating Writer-Critic Visual Workflow...");
    }

    /// <summary>
    /// Display workflow registration success message
    /// </summary>
    public static void ShowWorkflowRegistered()
    {
        Console.WriteLine("[SUCCESS] Writer-Critic workflow registered");
        Console.WriteLine("[INFO] Workflow structure: Writer → Critic");
        Console.WriteLine("[TIP] The workflow will appear as a visual graph in DevUI");
        Console.WriteLine();
    }

    /// <summary>
    /// Display web mode startup banner with all endpoint information
    /// </summary>
    public static void ShowWebModeBanner()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           ARCHITECT CENTRAL - AGENT STUDIO (Web Mode)                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"[WEB] API Endpoint:      https://localhost:5001");
        Console.WriteLine($"[UI]  Dev UI:            https://localhost:5001/devui");
        Console.WriteLine();
        Console.WriteLine("Available Endpoints:");
        Console.WriteLine("  GET  /health                    - Health check");
        Console.WriteLine("  POST /api/query                 - Query architecture docs (RAG)");
        Console.WriteLine("  POST /api/generate              - Generate agent artifacts");
        Console.WriteLine("  POST /api/workflow/generate     - Generate with Writer-Critic workflow");
        Console.WriteLine();
        Console.WriteLine("Agent Framework DevUI:");
        Console.WriteLine("  GET  /devui                     - Interactive agent testing UI");
        Console.WriteLine("  POST /v1/responses              - OpenAI-compatible responses API");
        Console.WriteLine("  POST /v1/conversations          - OpenAI-compatible conversations API");
        Console.WriteLine();
        Console.WriteLine("Available Agents in DevUI:");
        Console.WriteLine("  • architect-assistant           - Query architecture docs and get guidance");
        Console.WriteLine("  • agent-generator               - Generate Copilot agent artifacts");
        Console.WriteLine("  • rag-debugger                  - Debug RAG queries and results");
        Console.WriteLine("  • agent-writer                  - Agent file writer (workflow component)");
        Console.WriteLine("  • agent-critic                  - Agent file critic (workflow component)");
        Console.WriteLine("  • agent-generation-workflow     - Full Writer-Critic workflow");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop the server.");
        Console.WriteLine();
    }
}
