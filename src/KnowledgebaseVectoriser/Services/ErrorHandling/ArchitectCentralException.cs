namespace KnowledgebaseVectoriser.Services.ErrorHandling;

public class ArchitectCentralException : Exception
{
    public string ErrorCode { get; }
    public string? Context { get; }

    public ArchitectCentralException(string errorCode, string message, string? context = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Context = context;
    }

    public ArchitectCentralException(string errorCode, string message, Exception innerException, string? context = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Context = context;
    }
}

public class DocumentProcessingException : ArchitectCentralException
{
    public DocumentProcessingException(string message, string? filePath = null)
        : base("DOC_PROCESSING", message, filePath)
    {
    }

    public DocumentProcessingException(string message, Exception innerException, string? filePath = null)
        : base("DOC_PROCESSING", message, innerException, filePath)
    {
    }
}

public class EmbeddingException : ArchitectCentralException
{
    public EmbeddingException(string message, string? context = null)
        : base("EMBEDDING", message, context)
    {
    }

    public EmbeddingException(string message, Exception innerException, string? context = null)
        : base("EMBEDDING", message, innerException, context)
    {
    }
}

public class VectorDatabaseException : ArchitectCentralException
{
    public VectorDatabaseException(string message, string? context = null)
        : base("VECTOR_DB", message, context)
    {
    }

    public VectorDatabaseException(string message, Exception innerException, string? context = null)
        : base("VECTOR_DB", message, innerException, context)
    {
    }
}

public class RAGQueryException : ArchitectCentralException
{
    public RAGQueryException(string message, string? query = null)
        : base("RAG_QUERY", message, query)
    {
    }

    public RAGQueryException(string message, Exception innerException, string? query = null)
        : base("RAG_QUERY", message, innerException, query)
    {
    }
}

public class GenerationException : ArchitectCentralException
{
    public GenerationException(string message, string? context = null)
        : base("GENERATION", message, context)
    {
    }

    public GenerationException(string message, Exception innerException, string? context = null)
        : base("GENERATION", message, innerException, context)
    {
    }
}
