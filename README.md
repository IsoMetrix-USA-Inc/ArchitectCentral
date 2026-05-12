# 🏛️ ArchitectCentral

> **AI-powered architecture documentation ingestion, RAG-enabled agent framework for automated agent generation**

ArchitectCentral is a sophisticated platform that combines document vectorization, retrieval-augmented generation (RAG), and Microsoft Agent Framework to create intelligent agents that understand and work with your architecture documentation.

## 🎯 Overview

ArchitectCentral orchestrates the entire lifecycle of architecture-aware AI agents:

1. **📚 Document Ingestion** - Scans and parses architecture documentation (ADRs, guidelines, diagrams)
2. **🔍 Vectorization** - Chunks and embeds documents using Azure OpenAI for semantic search
3. **💾 Vector Storage** - Stores embeddings in PostgreSQL with pgvector for efficient retrieval
4. **🤖 Agent Generation** - Creates intelligent agents using Microsoft Agent Framework
5. **💬 Interactive Interfaces** - Provides both Terminal UI (Spectre.Console) and Web UI (DevUI)

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Terminal UI           Browser UI                           │
│  (Spectre.Console)     (DevUI - https://localhost:5001)     │
└────────────┬─────────────────────┬──────────────────────────┘
             │                     │
             ▼                     ▼
┌─────────────────────────────────────────────────────────────┐
│           ArchitectCentral.AgentStudio                      │
│  • architect-assistant  • agent-generator  • rag-debugger   │
│  • RAG Query Service    • Agent Generator Service           │
│  • Chat Service         • AI Function Tools                 │
└──────────────────┬──────────────────────┬───────────────────┘
                   │                      │
                   ▼                      ▼
┌──────────────────────────┐    ┌────────────────────────────┐
│ KnowledgebaseVectoriser  │    │      Azure OpenAI          │
│ • Document Scanning      │    │  • Chat Completions        │
│ • Markdown/ADR Parsing   │    │  • Text Embeddings         │
│ • Chunking Strategies    │    └────────────────────────────┘
│ • Embedding Service      │
│ • Vector DB Service      │
└────────┬─────────────────┘
         ▼
┌────────────────────────┐
│  PostgreSQL + pgvector │
│  • Document Chunks     │
│  • Vector Embeddings   │
└────────────────────────┘
```

## ✨ Features

### 🔬 Intelligent Document Processing
- **Multi-format Support**: ADRs, Guidelines, Diagrams
- **Smart Chunking**: Context-aware document segmentation
- **Section Preservation**: Maintains document structure and hierarchy
- **Metadata Extraction**: Captures document type, sections, and relationships

### 🧠 RAG-Enabled Query System
- **Semantic Search**: Vector similarity search for relevant documentation
- **Context Injection**: Enriches agent responses with architecture knowledge
- **Real-time Embeddings**: Azure OpenAI integration for high-quality embeddings

### 🤖 Agent Framework Integration
- **Microsoft Agent Framework**: Built on preview version with AI Workflows
- **Multi-Agent System**: 
  - `architect-assistant` - Architecture consultation
  - `agent-generator` - Automated .agent file generation
  - `rag-debugger` - Troubleshooting RAG queries
- **Function Tools**: Extensible AI function calling with AIFunctionFactory

### 🖥️ Dual Interface
- **Terminal UI**: Rich terminal experience with Spectre.Console
- **Web UI**: Browser-based DevUI for visual interaction
- **API Endpoints**: RESTful APIs for integration

## 🚀 Getting Started

### Prerequisites

- **.NET 10.0 SDK** or later
- **Docker Desktop** (for PostgreSQL)
- **Azure OpenAI** account with API key

### Quick Start

#### 1. Clone and Navigate
```bash
git clone https://github.com/StuartIsometrix/ArchitectCentral.git
cd ArchitectCentral
```

#### 2. Configure Azure OpenAI

Using User Secrets (recommended):
```bash
cd src/ArchitectCentral.AgentStudio
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "your-deployment-name"
```

#### 3. Start PostgreSQL with pgvector
```bash
cd src/KnowledgebaseVectoriser
docker-compose up -d

# Wait for database to be ready
docker-compose logs postgres
```

#### 4. Run with .NET Aspire

Using the AppHost orchestrator:
```bash
cd src/ArchitectCentral.AppHost
dotnet run
```

Or run services individually:

**Terminal UI Mode:**
```bash
cd src/ArchitectCentral.AgentStudio
dotnet run
```

**Web UI Mode:**
```bash
cd src/ArchitectCentral.AgentStudio
dotnet run --web
# Open https://localhost:5001/devui
```

## 📁 Project Structure

```
ArchitectCentral/
├── src/
│   ├── ArchitectCentral.AgentStudio/        # Main agent application
│   │   ├── Agents/                          # Agent definitions
│   │   ├── Services/                        # RAG, Chat, Generator services
│   │   ├── Tools/                           # AI function tools
│   │   └── ARCHITECTURE-DIAGRAM.md          # Detailed architecture
│   │
│   ├── ArchitectCentral.AppHost/            # .NET Aspire orchestrator
│   │   └── Program.cs                       # Service composition
│   │
│   ├── KnowledgebaseVectoriser/             # Document processing
│   │   ├── Services/
│   │   │   ├── Chunking/                    # Chunking strategies
│   │   │   ├── Metadata/                    # Metadata extraction
│   │   │   └── Query/                       # Vector search
│   │   └── GETTING-STARTED.md               # Setup guide
│   │
│   └── ArchitectCentral.ServiceDefaults/    # Shared configuration
│
├── ArchitectCentral.AgentStudio.sln         # Solution file
└── README.md                                 # This file
```

## 🛠️ Configuration

### Required Settings

**Azure OpenAI** (via User Secrets or appsettings):
```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4",
    "EmbeddingDeploymentName": "text-embedding-ada-002"
  }
}
```

**PostgreSQL Connection**:
```json
{
  "PostgreSQL": {
    "ConnectionString": "Host=localhost;Port=5433;Database=architect_central;Username=architect_admin;Password=architect_pass"
  }
}
```

## 📖 Usage Examples

### Query Architecture Documentation
```bash
# Using Terminal UI
dotnet run
> "What are our architecture principles?"

# Using API
curl -X POST https://localhost:5001/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "What are our architecture principles?"}'
```

### Generate Agent Artifacts
```bash
# Via agent-generator agent
> "Generate an agent file for API design review"
```

### Verify Vector Database
```bash
docker exec -it architect-central-postgres psql -U architect_admin -d architect_central

# Check document count
SELECT COUNT(*) FROM document_chunks;

# Search for content
SELECT chunk_id, section, content 
FROM document_chunks 
WHERE content ILIKE '%microservices%';
```

## 🧪 Development

### Build Solution
```bash
dotnet build ArchitectCentral.AgentStudio.sln
```

### Run Tests
```bash
dotnet test
```

### Database Migrations
```bash
cd src/KnowledgebaseVectoriser
# Schema is in init-db.sql, loaded automatically by docker-compose
```

## 🔧 Troubleshooting

### Common Issues

**Docker not running:**
```bash
# Start Docker Desktop, then verify
docker version
```

**Database connection refused:**
```bash
docker-compose down
docker-compose up -d
docker-compose logs postgres
```

**Azure OpenAI rate limits:**
- Built-in retry logic with exponential backoff
- Check your quota in Azure Portal

**Embeddings not generated:**
- Verify Azure OpenAI credentials
- Check deployment name matches your Azure resource

See [GETTING-STARTED.md](src/KnowledgebaseVectoriser/GETTING-STARTED.md) for detailed troubleshooting.

## 📦 Dependencies

### Key Technologies
- **.NET 10.0** - Application runtime
- **ASP.NET Core** - Web framework
- **Microsoft Agent Framework** (Preview) - Agent orchestration
- **Microsoft.Extensions.AI** - AI abstractions
- **Azure OpenAI** - LLM and embeddings
- **PostgreSQL** - Database
- **pgvector** - Vector similarity search
- **Npgsql** - PostgreSQL client
- **.NET Aspire** - Cloud-native orchestration
- **Spectre.Console** - Terminal UI
- **YamlDotNet** - YAML parsing
- **OpenTelemetry** - Observability

## 🤝 Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues.

## 📄 License

[Specify your license here]

## 👤 Author

**Stuart Wessels** - [StuartIsometrix](https://github.com/StuartIsometrix)

---

**Built with** ❤️ **using Microsoft Agent Framework, Azure OpenAI, and .NET 10**
