using ArchitectCentral.AgentStudio.Models.Agent;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchitectCentral.AgentStudio.Services.Agent;

/// <summary>
/// Service that loads agent definitions from markdown files with YAML frontmatter
/// </summary>
public class AgentDefinitionLoader
{
    private readonly ILogger<AgentDefinitionLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;

    public AgentDefinitionLoader(ILogger<AgentDefinitionLoader> logger)
    {
        _logger = logger;
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Load all agent definitions from a directory
    /// </summary>
    public async Task<List<AgentDefinition>> LoadFromDirectoryAsync(string directory)
    {
        var agents = new List<AgentDefinition>();

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Agent definitions directory not found: {Directory}", directory);
            return agents;
        }

        var files = Directory.GetFiles(directory, "*.md", SearchOption.TopDirectoryOnly);
        _logger.LogInformation("Found {Count} agent definition files in {Directory}", files.Length, directory);

        foreach (var file in files)
        {
            try
            {
                var agent = await LoadFromFileAsync(file);
                if (agent != null)
                {
                    agents.Add(agent);
                    _logger.LogInformation("Loaded agent definition: {Name} from {File}", agent.Name, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load agent definition from {File}", file);
            }
        }

        return agents;
    }

    /// <summary>
    /// Load a single agent definition from a markdown file
    /// </summary>
    public async Task<AgentDefinition?> LoadFromFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        return ParseMarkdownAgent(content, filePath);
    }

    /// <summary>
    /// Parse agent definition from markdown content with YAML frontmatter
    /// Expected format:
    /// ---
    /// name: agent-name
    /// description: Short description
    /// tools:
    ///   - tool1
    ///   - tool2
    /// conversation_starters:
    ///   - "Question 1"
    ///   - "Question 2"
    /// ---
    /// # Instructions
    /// Main agent instructions here...
    /// </summary>
    private AgentDefinition? ParseMarkdownAgent(string content, string sourceFile)
    {
        // Split frontmatter and body
        var parts = content.Split(["---"], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            _logger.LogWarning("Invalid agent definition format in {File}: missing frontmatter", sourceFile);
            return null;
        }

        // Parse YAML frontmatter
        var frontmatterYaml = parts[0].Trim();
        var instructions = parts[1].Trim();

        var frontmatter = _yamlDeserializer.Deserialize<Dictionary<string, object>>(frontmatterYaml);

        var agent = new AgentDefinition
        {
            SourceFile = sourceFile,
            Instructions = instructions
        };

        // Extract fields from frontmatter
        if (frontmatter.TryGetValue("name", out var name))
            agent.Name = name.ToString() ?? string.Empty;

        if (frontmatter.TryGetValue("description", out var description))
            agent.Description = description.ToString() ?? string.Empty;

        if (frontmatter.TryGetValue("tools", out var tools))
        {
            if (tools is List<object> toolList)
            {
                agent.ToolNames = toolList.Select(t => t.ToString() ?? string.Empty).ToList();
            }
        }

        if (frontmatter.TryGetValue("conversation_starters", out var starters))
        {
            if (starters is List<object> starterList)
            {
                agent.ConversationStarters = starterList.Select(s => s.ToString() ?? string.Empty).ToList();
            }
        }

        // Store all frontmatter as metadata for extensibility
        agent.Metadata = frontmatter.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToString() ?? string.Empty
        );

        return agent;
    }
}
