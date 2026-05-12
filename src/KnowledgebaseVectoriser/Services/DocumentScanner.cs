using KnowledgebaseVectoriser.Models;
using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services
{
    /// <summary>
    /// Service for discovering and scanning documentation files
    /// </summary>
    public class DocumentScanner
    {
        private readonly ILogger<DocumentScanner> _logger;
        private readonly string _docsPath;

        public DocumentScanner(ILogger<DocumentScanner> logger, string docsPath)
        {
            _logger = logger;
            _docsPath = ResolveDocsPath(docsPath);
        }

        /// <summary>
        /// Resolve docs path - check multiple locations for flexibility
        /// </summary>
        private string ResolveDocsPath(string configuredPath)
        {
            // Try paths in this order:
            // 1. Configured path as-is (relative to current directory)
            // 2. Relative to project root (../../docs)
            // 3. In the same directory as the executable
            
            var paths = new[]
            {
                Path.GetFullPath(configuredPath),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath)),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "docs"))
            };

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    _logger.LogDebug("Found docs path at: {Path}", path);
                    return path;
                }
            }

            // Return the first attempted path if none exist (will error later with clear message)
            return paths[0];
        }

        /// <summary>
        /// Scans the docs folder and returns all markdown files with metadata
        /// </summary>
        public async Task<List<DocumentInfo>> ScanDocumentsAsync()
        {
            _logger.LogInformation("Starting document scan from: {DocsPath}", _docsPath);

            if (!Directory.Exists(_docsPath))
            {
                _logger.LogError("Docs path does not exist: {DocsPath}", _docsPath);
                _logger.LogError("Current directory: {CurrentDir}", Directory.GetCurrentDirectory());
                _logger.LogError("Base directory: {BaseDir}", AppContext.BaseDirectory);
                throw new DirectoryNotFoundException($"Docs path not found: {_docsPath}");
            }

            var documents = new List<DocumentInfo>();

            // Recursively scan all .md files
            var markdownFiles = Directory.GetFiles(_docsPath, "*.md", SearchOption.AllDirectories);
            
            _logger.LogInformation("Found {Count} markdown files", markdownFiles.Length);

            foreach (var filePath in markdownFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    var relativePath = Path.GetRelativePath(_docsPath, filePath);
                    var docType = DetermineDocumentType(relativePath);

                    // Skip README files as they're typically indexes
                    if (fileInfo.Name.Equals("README.md", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Skipping README: {Path}", relativePath);
                        continue;
                    }

                    // Skip template files
                    if (fileInfo.Name.Equals("template.md", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("Skipping template: {Path}", relativePath);
                        continue;
                    }

                    var doc = new DocumentInfo
                    {
                        FilePath = filePath,
                        RelativePath = relativePath,
                        Type = docType,
                        FileName = fileInfo.Name,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        FileSize = fileInfo.Length
                    };

                    documents.Add(doc);
                    _logger.LogDebug("Discovered: {Document}", doc);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing file: {FilePath}", filePath);
                }
            }

            _logger.LogInformation("Document scan complete. Found {TotalCount} documents: {ADRCount} ADRs, {GuidelineCount} guidelines, {DiagramCount} diagrams, {OtherCount} other",
                documents.Count,
                documents.Count(d => d.Type == DocumentType.ADR),
                documents.Count(d => d.Type == DocumentType.Guideline),
                documents.Count(d => d.Type == DocumentType.Diagram),
                documents.Count(d => d.Type == DocumentType.Other));

            return documents;
        }

        /// <summary>
        /// Determines document type based on folder structure
        /// </summary>
        private DocumentType DetermineDocumentType(string relativePath)
        {
            var normalizedPath = relativePath.Replace("\\", "/").ToLowerInvariant();

            if (normalizedPath.StartsWith("adr/"))
                return DocumentType.ADR;

            if (normalizedPath.StartsWith("architecture/"))
                return DocumentType.Guideline;

            if (normalizedPath.StartsWith("diagrams/"))
                return DocumentType.Diagram;

            return DocumentType.Other;
        }

        /// <summary>
        /// Reads the content of a document file
        /// </summary>
        public async Task<string> ReadDocumentContentAsync(DocumentInfo document)
        {
            _logger.LogDebug("Reading document: {Path}", document.RelativePath);
            return await File.ReadAllTextAsync(document.FilePath);
        }
    }
}
