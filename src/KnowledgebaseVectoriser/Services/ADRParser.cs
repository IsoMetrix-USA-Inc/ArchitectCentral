using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Specialized parser for Architecture Decision Records (ADRs)
    /// </summary>
    public class ADRParser
    {
        private readonly ILogger<ADRParser> _logger;
        private readonly MarkdownParser _markdownParser;

        public ADRParser(ILogger<ADRParser> logger, MarkdownParser markdownParser)
        {
            _logger = logger;
            _markdownParser = markdownParser;
        }

        /// <summary>
        /// Parse an ADR document into structured format
        /// </summary>
        public ADRDocument Parse(string content, string filePath)
        {
            var parsed = _markdownParser.Parse(content);
            
            var adr = new ADRDocument
            {
                FilePath = filePath,
                FullContent = content,
                IndexedAt = DateTime.UtcNow
            };

            // Extract ADR number from filename (e.g., "0001-record-architecture-decisions.md")
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.Length >= 4 && int.TryParse(fileName.Substring(0, 4), out int adrNumber))
            {
                adr.Number = adrNumber;
                adr.Id = $"adr_{adrNumber:D4}";
            }
            else
            {
                adr.Id = $"adr_{fileName}";
                _logger.LogWarning("Could not extract ADR number from filename: {FileName}", fileName);
            }

            // Extract from frontmatter
            if (parsed.Frontmatter.TryGetValue("title", out var title))
                adr.Title = title;
            if (parsed.Frontmatter.TryGetValue("status", out var status))
                adr.Status = status;
            if (parsed.Frontmatter.TryGetValue("date", out var dateStr) && DateTime.TryParse(dateStr, out var date))
                adr.Date = date;

            // Extract title from first section or header
            if (string.IsNullOrEmpty(adr.Title) && parsed.Sections.Count > 0)
            {
                var firstSection = parsed.Sections.FirstOrDefault(s => s.Level == 1);
                if (firstSection != null)
                {
                    adr.Title = firstSection.Title;
                }
            }

            // Extract standard ADR sections
            foreach (var section in parsed.Sections)
            {
                var sectionTitle = section.Title.ToLowerInvariant().Trim();

                if (sectionTitle.Contains("status"))
                {
                    adr.Status = section.Content.Trim();
                }
                else if (sectionTitle.Contains("context"))
                {
                    adr.Context = section.Content.Trim();
                }
                else if (sectionTitle.Contains("decision"))
                {
                    adr.Decision = section.Content.Trim();
                }
                else if (sectionTitle.Contains("consequences") || sectionTitle.Contains("implications"))
                {
                    adr.Consequences = section.Content.Trim();
                }
                else if (sectionTitle.Contains("alternatives") || sectionTitle.Contains("options considered"))
                {
                    adr.Alternatives = section.Content.Trim();
                }
            }

            // Extract related ADRs from content
            var relatedAdrs = ExtractRelatedAdrs(content);
            if (relatedAdrs.Count > 0)
            {
                adr.RelatedAdrs = string.Join(",", relatedAdrs);
            }

            // Extract tags (could be from frontmatter or content)
            var tags = ExtractTags(parsed);
            if (tags.Count > 0)
            {
                adr.Tags = string.Join(",", tags);
            }

            _logger.LogDebug("Parsed ADR {Id}: {Title}", adr.Id, adr.Title);

            return adr;
        }

        /// <summary>
        /// Extract references to other ADRs (e.g., "ADR-0001", "0002")
        /// </summary>
        private List<string> ExtractRelatedAdrs(string content)
        {
            var related = new HashSet<string>();
            
            // Match patterns like "ADR-0001", "ADR 0001", "ADR-#0001"
            var adrPattern = @"ADR[-\s#]*(\d{4})";
            var matches = System.Text.RegularExpressions.Regex.Matches(content, adrPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    related.Add(match.Groups[1].Value);
                }
            }

            return related.ToList();
        }

        /// <summary>
        /// Extract tags from the ADR
        /// </summary>
        private List<string> ExtractTags(ParsedMarkdown parsed)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // From frontmatter
            if (parsed.Frontmatter.TryGetValue("tags", out var tagStr))
            {
                var tagArray = tagStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tagArray)
                {
                    tags.Add(tag.Trim());
                }
            }

            // Infer tags from content (common architectural keywords)
            var content = parsed.FullContent.ToLowerInvariant();
            var keywords = new[]
            {
                "authentication", "authorization", "database", "api", "frontend", "backend",
                "security", "performance", "scalability", "monitoring", "logging",
                "multi-tenant", "saga", "event-driven", "microservices", "monolith"
            };

            foreach (var keyword in keywords)
            {
                if (content.Contains(keyword))
                {
                    tags.Add(keyword);
                }
            }

            return tags.ToList();
        }
    }
}
