using ArchitectCentral.AgentStudio.Models.Agent;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ArchitectCentral.AgentStudio.Services.Agent;

/// <summary>
/// Service that registers agents with the hosting framework using definitions and DI-provided tools
/// </summary>
public class AgentRegistrationService
{
    private readonly ILogger<AgentRegistrationService> _logger;
    private readonly AgentDefinitionLoader _definitionLoader;

    public AgentRegistrationService(
        ILogger<AgentRegistrationService> logger,
        AgentDefinitionLoader definitionLoader)
    {
        _logger = logger;
        _definitionLoader = definitionLoader;
    }

    /// <summary>
    /// Register all agents from definitions directory with the WebApplicationBuilder
    /// </summary>
    public async Task RegisterAgentsAsync(
        WebApplicationBuilder builder,
        string definitionsDirectory,
        Dictionary<string, AIFunction> availableTools)
    {
        var definitions = await _definitionLoader.LoadFromDirectoryAsync(definitionsDirectory);
        
        _logger.LogInformation("Registering {Count} agents", definitions.Count);

        foreach (var definition in definitions)
        {
            try
            {
                RegisterAgent(builder, definition, availableTools);
                _logger.LogInformation("[SUCCESS] Registered agent: {Name}", definition.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERROR] Failed to register agent: {Name}", definition.Name);
            }
        }
    }

    /// <summary>
    /// Register a single agent from definition
    /// </summary>
    private void RegisterAgent(
        WebApplicationBuilder builder,
        AgentDefinition definition,
        Dictionary<string, AIFunction> availableTools)
    {
        // Get tools for this agent
        var agentTools = definition.ToolNames
            .Where(toolName => availableTools.ContainsKey(toolName))
            .Select(toolName => availableTools[toolName])
            .ToArray();

        if (agentTools.Length != definition.ToolNames.Count)
        {
            var missingTools = definition.ToolNames
                .Where(toolName => !availableTools.ContainsKey(toolName))
                .ToList();
            
            _logger.LogWarning(
                "Agent {Name} references missing tools: {MissingTools}",
                definition.Name,
                string.Join(", ", missingTools));
        }

        // Register agent with builder
        builder.AddAIAgent(definition.Name, definition.Instructions)
            .WithAITools(agentTools);

        _logger.LogInformation(
            "  Tools: {Tools}",
            string.Join(", ", definition.ToolNames));
    }
}
