using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services.Generation
{
    /// <summary>
    /// Service for generating .agent files with architectural context from RAG
    /// </summary>
    public class AgentFileGenerator
    {
        private readonly ILogger<AgentFileGenerator> _logger;
        private readonly RAGQueryService _ragService;

        public AgentFileGenerator(
            ILogger<AgentFileGenerator> logger,
            RAGQueryService ragService)
        {
            _logger = logger;
            _ragService = ragService;
        }

        /// <summary>
        /// Generate a .agent file based on project requirements
        /// </summary>
        public async Task<string> GenerateAgentFileAsync(AgentGenerationRequest request)
        {
            _logger.LogInformation("Generating .agent file for: {ProjectType}", request.ProjectType);

            // Build search query from requirements
            var searchQuery = BuildSearchQuery(request);
            
            _logger.LogDebug("Search query: {Query}", searchQuery);

            // Retrieve relevant architectural context
            var adrResults = await _ragService.SearchADRsAsync(searchQuery, topK: 5, similarityThreshold: 0.65);
            var guidelineResults = await _ragService.SearchGuidelinesAsync(searchQuery, topK: 5, similarityThreshold: 0.65);

            // Build content sections
            var architecturalContext = BuildArchitecturalContext(adrResults, guidelineResults);
            var relatedAdrs = BuildRelatedADRs(adrResults);
            var guidelines = BuildGuidelines(guidelineResults);
            var codeExamples = ExtractCodeExamples(guidelineResults);
            var instructions = BuildInstructions(request, adrResults, guidelineResults);

            // Generate agent file from template
            var agentContent = GenerationTemplates.AgentFileTemplate
                .Replace("{AGENT_NAME}", request.AgentName ?? $"{request.ProjectType} Development Agent")
                .Replace("{AGENT_DESCRIPTION}", request.Description ?? $"AI agent for developing {request.ProjectType} applications following Luminora architectural standards.")
                .Replace("{ARCHITECTURAL_CONTEXT}", architecturalContext)
                .Replace("{RELATED_ADRS}", relatedAdrs)
                .Replace("{GUIDELINES}", guidelines)
                .Replace("{CODE_EXAMPLES}", codeExamples)
                .Replace("{INSTRUCTIONS}", instructions);

            _logger.LogInformation("Generated .agent file with {ADRCount} ADRs and {GuidelineCount} guidelines",
                adrResults.Count, guidelineResults.Count);

            return agentContent;
        }

        private string BuildSearchQuery(AgentGenerationRequest request)
        {
            var queryParts = new List<string> { request.ProjectType };
            
            if (request.Technologies != null && request.Technologies.Count > 0)
            {
                queryParts.AddRange(request.Technologies);
            }

            if (request.Patterns != null && request.Patterns.Count > 0)
            {
                queryParts.AddRange(request.Patterns);
            }

            return string.Join(" ", queryParts);
        }

        private string BuildArchitecturalContext(List<SearchResult> adrResults, List<SearchResult> guidelineResults)
        {
            var context = new List<string>();

            if (adrResults.Count > 0)
            {
                context.Add("**Key Architectural Decisions:**");
                foreach (var adr in adrResults.Take(3))
                {
                    context.Add($"- {adr.SourceDocumentId}: {TruncateContent(adr.Content, 200)}");
                }
                context.Add("");
            }

            if (guidelineResults.Count > 0)
            {
                context.Add("**Applicable Guidelines:**");
                foreach (var guideline in guidelineResults.Take(3))
                {
                    context.Add($"- {guideline.SourceDocumentId}: {TruncateContent(guideline.Content, 200)}");
                }
            }

            return string.Join("\n", context);
        }

        private string BuildRelatedADRs(List<SearchResult> adrResults)
        {
            if (adrResults.Count == 0)
            {
                return "No specific ADRs found for this context.";
            }

            var adrs = new List<string>();
            foreach (var adr in adrResults)
            {
                var adrNumber = ExtractADRNumber(adr.SourceDocumentId);
                adrs.Add($"""
                    ### {adr.SourceDocumentId}
                    **Relevance**: {adr.Similarity:P1}
                    **Section**: {adr.Section}
                    
                    {TruncateContent(adr.Content, 400)}
                    
                    """);
            }

            return string.Join("\n", adrs);
        }

        private string BuildGuidelines(List<SearchResult> guidelineResults)
        {
            if (guidelineResults.Count == 0)
            {
                return "Follow general best practices for clean code and architecture.";
            }

            var guidelines = new List<string>();
            foreach (var guideline in guidelineResults)
            {
                guidelines.Add($"""
                    ### {guideline.SourceDocumentId}
                    **Section**: {guideline.Section}
                    
                    {TruncateContent(guideline.Content, 300)}
                    
                    """);
            }

            return string.Join("\n", guidelines);
        }

        private string ExtractCodeExamples(List<SearchResult> guidelineResults)
        {
            var examples = new List<string>();

            foreach (var guideline in guidelineResults)
            {
                // Look for code blocks in the content
                if (guideline.Content.Contains("```"))
                {
                    examples.Add($"""
                        **From {guideline.SourceDocumentId}:**
                        
                        {guideline.Content}
                        
                        """);
                }
            }

            return examples.Count > 0 
                ? string.Join("\n", examples) 
                : "See guidelines above for code patterns and examples.";
        }

        private string BuildInstructions(
            AgentGenerationRequest request,
            List<SearchResult> adrResults,
            List<SearchResult> guidelineResults)
        {
            var instructions = new List<string>();

            // Add technology-specific instructions
            if (request.Technologies != null)
            {
                foreach (var tech in request.Technologies)
                {
                    instructions.Add($"- Use {tech} according to project standards");
                }
            }

            // Add pattern-specific instructions
            if (request.Patterns != null)
            {
                foreach (var pattern in request.Patterns)
                {
                    instructions.Add($"- Follow {pattern} architectural pattern");
                }
            }

            // Add ADR-based instructions
            var adrIds = adrResults.Select(r => r.SourceDocumentId).Distinct().Take(3);
            if (adrIds.Any())
            {
                instructions.Add($"- Align implementation with: {string.Join(", ", adrIds)}");
            }

            // Add guideline-based instructions
            var guidelineIds = guidelineResults.Select(r => r.SourceDocumentId).Distinct().Take(3);
            if (guidelineIds.Any())
            {
                instructions.Add($"- Follow best practices from: {string.Join(", ", guidelineIds)}");
            }

            return string.Join("\n", instructions);
        }

        private string ExtractADRNumber(string sourceDocId)
        {
            // Extract number from format like "adr_0008"
            var parts = sourceDocId.Split('_');
            return parts.Length > 1 ? parts[1] : "???";
        }

        private string TruncateContent(string content, int maxLength)
        {
            if (content.Length <= maxLength)
                return content;

            return content.Substring(0, maxLength) + "...";
        }
    }

    /// <summary>
    /// Request for generating an agent file
    /// </summary>
    public class AgentGenerationRequest
    {
        public string ProjectType { get; set; } = string.Empty;
        public string? AgentName { get; set; }
        public string? Description { get; set; }
        public List<string>? Technologies { get; set; }
        public List<string>? Patterns { get; set; }
    }
}
