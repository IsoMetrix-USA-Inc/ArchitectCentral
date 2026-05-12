using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Parsed markdown document with structured content
    /// </summary>
    public class ParsedMarkdown
    {
        public Dictionary<string, string> Frontmatter { get; set; } = new();
        public List<MarkdownSection> Sections { get; set; } = new();
        public string FullContent { get; set; } = string.Empty;
        public string RawContent { get; set; } = string.Empty;
    }

    /// <summary>
    /// A section of markdown content (based on headers)
    /// </summary>
    public class MarkdownSection
    {
        public string Title { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Content { get; set; } = string.Empty;
        public List<string> CodeBlocks { get; set; } = new();
        public int StartLine { get; set; }
        public int EndLine { get; set; }
    }

    /// <summary>
    /// Service for parsing markdown documents using Markdig
    /// </summary>
    public class MarkdownParser
    {
        private readonly ILogger<MarkdownParser> _logger;
        private readonly MarkdownPipeline _pipeline;
        private readonly IDeserializer _yamlDeserializer;

        public MarkdownParser(ILogger<MarkdownParser> _logger)
        {
            this._logger = _logger;
            _pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        /// <summary>
        /// Parse a markdown document into structured sections
        /// </summary>
        public ParsedMarkdown Parse(string markdownContent)
        {
            var result = new ParsedMarkdown
            {
                RawContent = markdownContent
            };

            // Extract frontmatter if present
            var contentAfterFrontmatter = ExtractFrontmatter(markdownContent, result.Frontmatter);

            // Parse with Markdig
            var document = Markdown.Parse(contentAfterFrontmatter, _pipeline);

            // Extract plain text for full content
            result.FullContent = document.ToPlainText();

            // Build sections based on headers
            var currentSection = new MarkdownSection
            {
                Title = "Preamble",
                Level = 0,
                StartLine = 0
            };

            var lines = contentAfterFrontmatter.Split('\n');
            int lineIndex = 0;

            foreach (var block in document)
            {
                if (block is HeadingBlock heading)
                {
                    // Save previous section
                    if (currentSection.Content.Length > 0 || currentSection.CodeBlocks.Count > 0)
                    {
                        currentSection.EndLine = lineIndex - 1;
                        result.Sections.Add(currentSection);
                    }

                    // Start new section
                    currentSection = new MarkdownSection
                    {
                        Title = heading.Inline?.ToPlainText() ?? "Untitled",
                        Level = heading.Level,
                        StartLine = lineIndex
                    };

                    lineIndex += CountLines(block);
                }
                else if (block is FencedCodeBlock codeBlock)
                {
                    var code = codeBlock.Lines.ToString();
                    currentSection.CodeBlocks.Add(code);
                    currentSection.Content += $"\n[Code Block: {codeBlock.Info}]\n";
                    lineIndex += CountLines(block);
                }
                else if (block is ParagraphBlock paragraph)
                {
                    var text = paragraph.Inline?.ToPlainText() ?? string.Empty;
                    currentSection.Content += text + "\n\n";
                    lineIndex += CountLines(block);
                }
                else if (block is ListBlock list)
                {
                    var listText = list.ToPlainText();
                    currentSection.Content += listText + "\n\n";
                    lineIndex += CountLines(block);
                }
                else
                {
                    lineIndex += CountLines(block);
                }
            }

            // Add final section
            if (currentSection.Content.Length > 0 || currentSection.CodeBlocks.Count > 0)
            {
                currentSection.EndLine = lines.Length - 1;
                result.Sections.Add(currentSection);
            }

            _logger.LogDebug("Parsed markdown into {SectionCount} sections", result.Sections.Count);

            return result;
        }

        /// <summary>
        /// Extract YAML frontmatter from markdown
        /// </summary>
        private string ExtractFrontmatter(string content, Dictionary<string, string> frontmatter)
        {
            if (!content.StartsWith("---"))
                return content;

            var lines = content.Split('\n');
            var frontmatterLines = new List<string>();
            var contentLines = new List<string>();
            bool inFrontmatter = false;
            bool frontmatterComplete = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (i == 0 && line.Trim() == "---")
                {
                    inFrontmatter = true;
                    continue;
                }

                if (inFrontmatter && line.Trim() == "---")
                {
                    inFrontmatter = false;
                    frontmatterComplete = true;
                    continue;
                }

                if (inFrontmatter)
                {
                    frontmatterLines.Add(line);
                }
                else
                {
                    contentLines.Add(line);
                }
            }

            // Parse YAML frontmatter
            if (frontmatterComplete && frontmatterLines.Count > 0)
            {
                try
                {
                    var yaml = string.Join("\n", frontmatterLines);
                    var parsed = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yaml);
                    
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                        {
                            frontmatter[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                        }
                    }

                    _logger.LogDebug("Extracted {Count} frontmatter properties", frontmatter.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse YAML frontmatter");
                }

                return string.Join("\n", contentLines);
            }

            return content;
        }

        private int CountLines(Block block)
        {
            if (block.Line >= 0 && block.Line <= block.Span.End)
            {
                return Math.Max(1, block.Span.End - block.Line + 1);
            }
            return 1;
        }
    }

    /// <summary>
    /// Extension methods for Markdig
    /// </summary>
    public static class MarkdigExtensions
    {
        public static string ToPlainText(this Markdig.Syntax.Inlines.ContainerInline inline)
        {
            if (inline == null) return string.Empty;

            var text = new System.Text.StringBuilder();
            foreach (var child in inline)
            {
                if (child is Markdig.Syntax.Inlines.LiteralInline literal)
                {
                    text.Append(literal.Content.ToString());
                }
                else if (child is Markdig.Syntax.Inlines.ContainerInline container)
                {
                    text.Append(container.ToPlainText());
                }
            }
            return text.ToString();
        }

        public static string ToPlainText(this MarkdownDocument document)
        {
            var text = new System.Text.StringBuilder();
            foreach (var block in document)
            {
                text.AppendLine(block.ToPlainText());
            }
            return text.ToString();
        }

        public static string ToPlainText(this Block block)
        {
            return block switch
            {
                HeadingBlock heading => heading.Inline?.ToPlainText() ?? string.Empty,
                ParagraphBlock paragraph => paragraph.Inline?.ToPlainText() ?? string.Empty,
                ListBlock list => list.ToPlainText(),
                FencedCodeBlock code => code.Lines.ToString(),
                _ => string.Empty
            };
        }

        public static string ToPlainText(this ListBlock list)
        {
            var text = new System.Text.StringBuilder();
            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    foreach (var block in listItem)
                    {
                        text.AppendLine("- " + block.ToPlainText());
                    }
                }
            }
            return text.ToString();
        }
    }
}
