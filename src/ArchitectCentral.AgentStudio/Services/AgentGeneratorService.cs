using ArchitectCentral.AgentStudio.Models.Artifacts;
using ArchitectCentral.AgentStudio.Models.Codebase;
using ArchitectCentral.AgentStudio.Models.RAG;
using System.Text;

namespace ArchitectCentral.AgentStudio.Services;

/// <summary>
/// Generates GitHub Copilot agent artifacts (agents, skills, instructions) based on codebase analysis
/// </summary>
public class AgentGeneratorService
{
    private readonly ILogger<AgentGeneratorService> _logger;
    private readonly ChatService _chatService;
    private readonly EnhancedRAGQueryService _enhancedRagService;

    public AgentGeneratorService(
        ILogger<AgentGeneratorService> logger,
        ChatService chatService,
        EnhancedRAGQueryService enhancedRagService)
    {
        _logger = logger;
        _chatService = chatService;
        _enhancedRagService = enhancedRagService;
    }

    public async Task<GeneratedArtifacts> GenerateArtifactsAsync(CodebaseRequest codebase)
    {
        _logger.LogInformation("Generating agent artifacts for: {Name}", codebase.Name);

        // Step 1: Query RAG for relevant architecture patterns
        var ragResults = await QueryRelevantPatternsAsync(codebase);

        // Step 2: Generate agent file (.agent)
        var agentFile = await GenerateAgentFileAsync(codebase, ragResults);

        // Step 3: Generate skills
        var skills = await GenerateSkillsAsync(codebase, ragResults);

        // Step 4: Generate instructions
        var instructions = await GenerateInstructionsAsync(codebase, ragResults);

        // Combine all artifact files
        var allFiles = new List<ArtifactFile>();
        allFiles.AddRange(skills);
        allFiles.AddRange(instructions);

        return new GeneratedArtifacts
        {
            AgentFile = agentFile,
            Files = allFiles
        };
    }

    private async Task<List<RAGResult>> QueryRelevantPatternsAsync(CodebaseRequest codebase)
    {
        // Build comprehensive query based on codebase characteristics
        var queryParts = new List<string>
        {
            codebase.Runtime,
            codebase.Framework
        };

        // Add key dependencies
        queryParts.AddRange(codebase.Dependencies
            .Where(d => IsArchitecturallySignificant(d.Role))
            .Select(d => d.Name));

        var query = string.Join(" ", queryParts);
        _logger.LogInformation("RAG Query: {Query}", query);

        return await _enhancedRagService.QueryAsync(query);
    }

    private static bool IsArchitecturallySignificant(string role)
    {
        var significantKeywords = new[] { "framework", "orm", "pattern", "architecture", "auth", "cqrs", "mediator" };
        return significantKeywords.Any(keyword => role.Contains(keyword, StringComparison.InvariantCultureIgnoreCase));
    }

    private async Task<string> GenerateAgentFileAsync(CodebaseRequest codebase, List<RAGResult> ragContext)
    {
        var systemPrompt = @"You are an expert at creating GitHub Copilot agent files (.agent) for development teams.

        Your task is to create a comprehensive .agent file that will help developers work with the specified codebase while adhering to the organization's architecture principles.
        
        The .agent file should include:
        1. A clear name and description
        2. Instructions for how to work with this specific codebase
        3. References to relevant skills (if any)
        4. References to relevant instruction files
        5. Best practices from the architecture documentation
        
        Format the output as a complete .agent file (markdown format).";

        var userPrompt = BuildCodebaseDescription(codebase);

        var response = await _chatService.SendMessageAsync(userPrompt, systemPrompt, ragContext);
        return response;
    }

    private async Task<List<ArtifactFile>> GenerateSkillsAsync(CodebaseRequest codebase, List<RAGResult> ragContext)
    {
        var skills = new List<ArtifactFile>();

        // Determine what skills are needed based on the codebase
        var skillsNeeded = DetermineRequiredSkills(codebase);

        foreach (var skillName in skillsNeeded)
        {
            var systemPrompt = $@"You are an expert at creating GitHub Copilot skills for development teams.

Your task is to create a skill named '{skillName}' that will help developers work with {codebase.Name}.

The skill should:
1. Have a clear, focused purpose
2. Include step-by-step instructions
3. Reference relevant architecture patterns from the documentation
4. Include examples if appropriate

Format the output as a complete skill file (markdown format).";

            var userPrompt = $"Create a '{skillName}' skill for {codebase.Name}. " +
                           $"This is a {codebase.Runtime} {codebase.Framework} application.";

            var response = await _chatService.SendMessageAsync(userPrompt, systemPrompt, ragContext);

            skills.Add(new ArtifactFile
            {
                FileName = $"{ConvertToKebabCase(skillName)}.skill.md",
                Content = response,
                Type = ArtifactType.Skill
            });
        }

        return skills;
    }

    private List<string> DetermineRequiredSkills(CodebaseRequest codebase)
    {
        var skills = new List<string>();

        // Always include these base skills
        skills.Add("create-new-feature");
        skills.Add("implement-endpoint");

        // Add CQRS skill if MediatR is present
        if (codebase.Dependencies.Any(d => d.Name.Contains("MediatR", StringComparison.OrdinalIgnoreCase)))
        {
            skills.Add("implement-cqrs-handler");
        }

        // Add EF Core skill if Entity Framework is present
        if (codebase.Dependencies.Any(d => d.Name.Contains("Entity", StringComparison.OrdinalIgnoreCase)))
        {
            skills.Add("add-entity-migration");
        }

        // Add testing skill
        skills.Add("write-unit-tests");

        return skills;
    }

    private async Task<List<ArtifactFile>> GenerateInstructionsAsync(CodebaseRequest codebase, List<RAGResult> ragContext)
    {
        var instructions = new List<ArtifactFile>();

        // Generate coding standards instruction
        var codingStandardsPrompt = $@"You are creating an instructions file for coding standards specific to {codebase.Name}.

Based on the architecture documentation provided, create a comprehensive coding standards instruction file that covers:
1. Project structure and organization
2. Naming conventions
3. Error handling patterns
4. Logging and monitoring
5. Testing requirements
6. Security considerations

Format as a markdown instruction file.";

        var codingStandardsResponse = await _chatService.SendMessageAsync(
            $"Create coding standards instructions for {codebase.Name}",
            codingStandardsPrompt,
            ragContext);

        instructions.Add(new ArtifactFile
        {
            FileName = "coding-standards.instructions.md",
            Content = codingStandardsResponse,
            Type = ArtifactType.Instruction
        });

        // Generate architecture patterns instruction
        var architecturePrompt = $@"You are creating an instructions file for architecture patterns specific to {codebase.Name}.

Based on the architecture documentation provided, create an architecture patterns instruction file that covers:
1. Architectural decisions (ADRs) that apply to this codebase
2. Design patterns to use
3. Layer responsibilities
4. Data access patterns
5. API design principles

Format as a markdown instruction file.";

        var architectureResponse = await _chatService.SendMessageAsync(
            $"Create architecture pattern instructions for {codebase.Name}",
            architecturePrompt,
            ragContext);

        instructions.Add(new ArtifactFile
        {
            FileName = "architecture-patterns.instructions.md",
            Content = architectureResponse,
            Type = ArtifactType.Instruction
        });

        return instructions;
    }

    private static string BuildCodebaseDescription(CodebaseRequest codebase)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Codebase: {codebase.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Description**: {codebase.Description}");
        sb.AppendLine($"**Runtime**: {codebase.Runtime}");
        sb.AppendLine($"**Framework**: {codebase.Framework}");
        sb.AppendLine();
        sb.AppendLine("## Key Dependencies:");
        sb.AppendLine();

        foreach (var dep in codebase.Dependencies.Take(10)) // Top 10 most important
        {
            sb.AppendLine($"- **{dep.Name}** (v{dep.Version}): {dep.Role}");
        }

        return sb.ToString();
    }

    private static string ConvertToKebabCase(string text)
    {
        return text.ToLowerInvariant().Replace(" ", "-");
    }
}
