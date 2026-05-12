namespace KnowledgebaseVectoriser.Models
{
    /// <summary>
    /// Types of documents in the architecture repository
    /// </summary>
    public enum DocumentType
    {
        ADR,
        Guideline,
        Diagram,
        Other
    }

    /// <summary>
    /// Information about a discovered documentation file
    /// </summary>
    public class DocumentInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DocumentType Type { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public long FileSize { get; set; }

        public override string ToString()
        {
            return $"{Type}: {RelativePath}";
        }
    }
}
