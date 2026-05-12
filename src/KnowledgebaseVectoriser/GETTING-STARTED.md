# 🚀 Architect Central - Getting Started

## Prerequisites

1. **Docker Desktop** - Must be running
2. **.NET 10 SDK** - Already installed
3. **Azure OpenAI API Key** - Required for embeddings

## Setup Steps

### 1. Start Docker Desktop

Make sure Docker Desktop is running. You can check with:
```bash
docker version
```

### 2. Configure Azure OpenAI Credentials

The application needs Azure OpenAI credentials for generating embeddings. You have two options:

#### Option A: User Secrets (Recommended for development)
```bash
cd C:\Stuart\Lumina\Projects\LuminoraArchitecture\src\KnowledgebaseVectoriser

# Initialize user secrets
dotnet user-secrets init

# Set your Azure OpenAI credentials
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource-name.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"
```

#### Option B: Local Configuration File (Not committed to Git)
Create `appsettings.local.json`:
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource-name.openai.azure.com/",
    "ApiKey": "your-api-key-here"
  }
}
```

### 3. Start PostgreSQL Database

```bash
cd C:\Stuart\Lumina\Projects\LuminoraArchitecture\src\KnowledgebaseVectoriser
docker-compose up -d
```

Wait 10 seconds for the database to initialize, then verify:
```bash
docker-compose ps
docker-compose logs postgres
```

You should see: "database system is ready to accept connections"

### 4. Run the Application

```bash
dotnet run
```

The application will:
1. Scan documentation files
2. Parse and chunk them
3. Generate embeddings using Azure OpenAI
4. Store in the vector database
5. Run sample queries

## Verification

### Check Docker Container
```bash
# Check if running
docker-compose ps

# View logs
docker-compose logs -f postgres

# Connect to database
docker exec -it architect-central-postgres psql -U architect_admin -d architect_central
```

### Inside PostgreSQL
```sql
-- Verify pgvector extension
SELECT * FROM pg_extension WHERE extname = 'vector';

-- Check tables
\dt

-- Count chunks
SELECT COUNT(*) FROM document_chunks;

-- View sample chunks
SELECT chunk_id, doc_type, section, LENGTH(content) as content_len 
FROM document_chunks 
LIMIT 5;

-- Check embeddings
SELECT chunk_id, vector_dims(embedding) as dims 
FROM document_chunks 
WHERE embedding IS NOT NULL 
LIMIT 3;
```

## Troubleshooting

### Docker Desktop Not Running
**Error**: `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified`

**Solution**: Start Docker Desktop from the Windows Start menu and wait for it to fully start.

### Database Connection Failed
**Error**: `No connection could be made because the target machine actively refused it`

**Solution**: 
```bash
# Stop and restart
docker-compose down
docker-compose up -d

# Wait 10 seconds and check logs
docker-compose logs postgres
```

### Azure OpenAI Configuration Missing
**Error**: `The Azure OpenAI endpoint or API key is not configured`

**Solution**: Follow Step 2 above to configure credentials using user secrets.

### Rate Limiting
**Error**: `429 Too Many Requests`

**Solution**: The embedding service has built-in retry logic with delays. Large document sets may take time.

## Stopping the Application

### Stop Docker Container
```bash
docker-compose down
```

### Stop and Remove All Data (Clean Slate)
```bash
docker-compose down -v
```

## Next Steps

Once the application runs successfully:

1. **Review chunk quality** - Check that sections are preserved
2. **Test searches** - Try querying the vector database
3. **Generate agent files** - Use RAG to create .agent files based on architecture docs

## File Structure
```
src/KnowledgebaseVectoriser/
├── docker-compose.yml         # PostgreSQL with pgvector
├── init-db.sql                # Database schema
├── appsettings.json           # Configuration (no secrets!)
├── Program.cs                 # Application entry point
├── Models/                    # Data models
├── Services/                  # Core services
│   ├── DocumentScanner.cs
│   ├── MarkdownParser.cs
│   ├── ADRParser.cs
│   ├── DocumentProcessor.cs
│   ├── EmbeddingService.cs
│   └── VectorDatabaseService.cs
└── Services/Chunking/         # Chunking strategies
    ├── IChunkingStrategy.cs
    ├── ADRChunkingStrategy.cs
    ├── GuidelineChunkingStrategy.cs
    └── DiagramChunkingStrategy.cs
```

## Support

If you encounter issues:
1. Check Docker Desktop is running
2. Verify database logs: `docker-compose logs postgres`
3. Check application logs in the console
4. Ensure Azure OpenAI credentials are configured
