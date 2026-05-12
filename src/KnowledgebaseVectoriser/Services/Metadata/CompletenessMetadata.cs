namespace KnowledgebaseVectoriser.Services.Metadata
{
    /// <summary>
    /// Completeness indicators for a chunk
    /// </summary>
    public class CompletenessMetadata
    {
        public bool HasContext { get; set; }
        public bool HasExamples { get; set; }
        public bool HasConsequences { get; set; }
        public bool LengthAdequate { get; set; }
        public bool HasStructure { get; set; }
    }
}
