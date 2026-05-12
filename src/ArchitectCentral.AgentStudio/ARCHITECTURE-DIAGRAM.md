# Agent Studio - Visual Flow Diagram

## High-Level Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                         USER INTERFACES                           │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────┐         ┌──────────────────────────────┐   │
│  │  Terminal (TUI) │         │   Browser (DevUI)            │   │
│  │  Spectre.Console│         │   https://localhost:5001/devui│   │
│  └────────┬────────┘         └──────────────┬───────────────┘   │
│           │                                  │                    │
└───────────┼──────────────────────────────────┼────────────────────┘
            │                                  │
            │  dotnet run                      │  HTTP/HTTPS
            │  (no --web)                      │
            │                                  │
┌───────────▼──────────────────────────────────▼────────────────────┐
│                    ARCHITECT CENTRAL - AGENT STUDIO               │
│                     ASP.NET Core Web Application                  │
├───────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                    ROUTING LAYER                             │ │
│  │                                                              │ │
│  │  TUI Mode:                  Web Mode:                       │ │
│  │  • InteractiveMenu          • MapDevUI()                    │ │
│  │  • Spectre prompts          • MapOpenAIResponses()          │ │
│  │                             • MapOpenAIConversations()      │ │
│  │                             • /api/query                     │ │
│  │                             • /api/generate                  │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                    │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │               MICROSOFT AGENT FRAMEWORK                      │ │
│  │                                                              │ │
│  │  ┌───────────────┐  ┌──────────────┐  ┌─────────────────┐ │ │
│  │  │  architect-   │  │   agent-     │  │   rag-          │ │ │
│  │  │  assistant    │  │   generator  │  │   debugger      │ │ │
│  │  │               │  │              │  │                 │ │ │
│  │  │ System Prompt │  │System Prompt │  │ System Prompt   │ │ │
│  │  │ + Tools       │  │ + Tools      │  │ + Tools         │ │ │
│  │  └───────┬───────┘  └──────┬───────┘  └────────┬────────┘ │ │
│  │          │                  │                   │           │ │
│  │          └──────────────────┴───────────────────┘           │ │
│  │                              │                               │ │
│  │                              ▼                               │ │
│  │          ┌────────────────────────────────────────┐         │ │
│  │          │      AI FUNCTION TOOLS                 │         │ │
│  │          │                                        │         │ │
│  │          │  • query_architecture_docs             │         │ │
│  │          │  • generate_agent_artifacts            │         │ │
│  │          │  • get_current_time                    │         │ │
│  │          │                                        │         │ │
│  │          │  (AIFunctionFactory.Create wrappers)  │         │ │
│  │          └────────────────┬───────────────────────┘         │ │
│  └───────────────────────────┼─────────────────────────────────┘ │
│                              │                                    │
│  ┌───────────────────────────▼─────────────────────────────────┐ │
│  │                   YOUR SERVICES LAYER                        │ │
│  │                                                              │ │
│  │  ┌──────────────────┐  ┌─────────────────────────────────┐ │ │
│  │  │ RAGQueryService  │  │  AgentGeneratorService          │ │ │
│  │  │                  │  │                                 │ │ │
│  │  │ • QueryAsync()   │  │  • GenerateArtifactsAsync()     │ │ │
│  │  │ • Vector search  │  │  • Generate .agent files        │ │ │
│  │  │ • Embeddings     │  │  • Generate skills              │ │ │
│  │  └────────┬─────────┘  │  • Generate instructions        │ │ │
│  │           │             └────────────┬────────────────────┘ │ │
│  │           │                          │                      │ │
│  │  ┌────────▼──────────────────────────▼─────┐              │ │
│  │  │        ChatService                       │              │ │
│  │  │                                          │              │ │
│  │  │  • SendMessageAsync()                   │              │ │
│  │  │  • Conversation history                 │              │ │
│  │  │  • RAG context injection                │              │ │
│  │  └──────────────────────────────────────────┘              │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          │                   │                   │
          ▼                   ▼                   ▼
┌──────────────────┐  ┌──────────────┐  ┌────────────────┐
│   PostgreSQL     │  │ Azure OpenAI │  │  Configuration │
│   + pgvector     │  │              │  │  appsettings   │
│                  │  │ • Chat API   │  │  User Secrets  │
│ • document_chunks│  │ • Embeddings │  └────────────────┘
│ • Vector search  │  └──────────────┘
└──────────────────┘
```

## Agent Conversation Flow

```
1. User Message
   │
   ├─ Browser: User types in DevUI
   │  "What are our authentication patterns?"
   │
   └─ Terminal: User selects from TUI menu
      "Query architecture documentation"

2. Request Routing
   │
   ├─ Web Mode: POST /v1/responses
   │  {
   │    "model": "architect-assistant",
   │    "messages": [{"role": "user", "content": "..."}]
   │  }
   │
   └─ TUI Mode: Direct service call
      ragService.QueryAsync("authentication")

3. Agent Processing (Web Mode Only)
   │
   ├─ architect-assistant receives message
   │  └─ Analyzes: "User wants authentication info"
   │
   ├─ Decides to call query_architecture_docs tool
   │  └─ Parameters: { "query": "authentication patterns" }
   │
   └─ Agent Framework invokes tool

4. Tool Execution
   │
   ├─ AIFunctionFactory.Create(QueryArchitectureDocs)
   │  └─ Resolves IServiceProvider
   │
   ├─ Gets RAGQueryService from DI
   │  └─ Calls: ragService.QueryAsync("authentication patterns")
   │
   └─ RAGQueryService executes:
      ├─ Generate embedding via Azure OpenAI
      ├─ Query PostgreSQL with vector similarity
      └─ Return top K results with similarity scores

5. Result Processing
   │
   ├─ Tool function formats results:
   │  "Found 5 relevant documents:
   │   📄 ADR-012 - OAuth2 Implementation (Similarity: 94%)
   │   📄 Security Guidelines - Auth (Similarity: 89%)
   │   ..."
   │
   └─ Result returned to agent

6. Agent Response Generation
   │
   ├─ Agent receives tool results
   │  └─ Incorporates into context
   │
   ├─ Calls Azure OpenAI Chat API
   │  └─ Synthesizes answer based on:
   │      • Original user question
   │      • Retrieved documentation
   │      • System prompt instructions
   │
   └─ Generates final response

7. Response Display
   │
   ├─ DevUI: Shows conversation with tool calls
   │  ┌────────────────────────────────────────┐
   │  │ User: What are our auth patterns?      │
   │  │                                        │
   │  │ 🔧 Tool Call: query_architecture_docs  │
   │  │    query: "authentication patterns"    │
   │  │                                        │
   │  │ 📄 Tool Result: Found 5 documents...   │
   │  │                                        │
   │  │ Assistant: Based on our documentation, │
   │  │ we use OAuth2 with JWT tokens. See    │
   │  │ ADR-012 for implementation details...  │
   │  └────────────────────────────────────────┘
   │
   └─ TUI: Prints formatted results
      [Architecture Query Results]
      Found 5 matching documents
      • ADR-012: OAuth2 Implementation
      ...
```

## Tool Call Detail Flow

```
┌─────────────────────────────────────────────────────────────┐
│                  Agent Decides to Call Tool                  │
│                                                              │
│  architect-assistant: "I need to query the docs"            │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Agent Framework Tool Invocation                 │
│                                                              │
│  AIFunctionFactory.Create(QueryArchitectureDocs)            │
│  • Resolves function delegate                               │
│  • Prepares parameters from agent's decision                │
│  • Injects IServiceProvider                                 │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    Tool Function Executes                    │
│                                                              │
│  string QueryArchitectureDocs(                              │
│      string query,                                          │
│      IServiceProvider serviceProvider)                      │
│  {                                                          │
│      var ragService = serviceProvider                       │
│          .GetRequiredService<RAGQueryService>();            │
│                                                              │
│      var results = ragService                               │
│          .QueryAsync(query)                                 │
│          .GetAwaiter()                                      │
│          .GetResult();                                      │
│                                                              │
│      return FormatResults(results);                         │
│  }                                                          │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  RAGQueryService.QueryAsync                  │
│                                                              │
│  1. Generate embedding                                      │
│     └─ Azure OpenAI Embeddings API                          │
│        float[] queryVector = ...                            │
│                                                              │
│  2. Vector similarity search                                │
│     └─ PostgreSQL with pgvector                             │
│        SELECT * FROM document_chunks                        │
│        WHERE (1 - (embedding <=> @vector)) >= 0.70          │
│        ORDER BY embedding <=> @vector                       │
│        LIMIT 5                                              │
│                                                              │
│  3. Return results                                          │
│     └─ List<RAGResult> with:                                │
│        • Document content                                   │
│        • Similarity scores                                  │
│        • Metadata                                           │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   Result Formatting                          │
│                                                              │
│  "Found 5 relevant documents:                               │
│                                                              │
│  📄 ADR-012 - OAuth2 (Similarity: 94%)                       │
│  Our OAuth2 implementation uses...                          │
│                                                              │
│  📄 Security Guide (Similarity: 89%)                         │
│  Authentication best practices...                           │
│  ..."                                                       │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Result Returned to Agent                        │
│                                                              │
│  architect-assistant receives formatted string              │
│  • Sees document titles and scores                          │
│  • Has content snippets                                     │
│  • Can incorporate into response                            │
└───────────────────────────┬──────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│            Agent Generates Final Response                    │
│                                                              │
│  • Analyzes tool result                                     │
│  • Combines with user question                              │
│  • Crafts comprehensive answer                              │
│  • References specific documents                            │
│  • Provides actionable guidance                             │
└─────────────────────────────────────────────────────────────┘
```

## Deployment Modes Comparison

```
┌──────────────────────────────────────────────────────────────┐
│                      TUI MODE                                 │
│                   dotnet run                                  │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Spectre.Console Interactive Menu                   │    │
│  │                                                      │    │
│  │  1. Query Architecture Documentation                │    │
│  │  2. Generate Agent Artifacts                        │    │
│  │  3. Interactive Chat                                │    │
│  │  4. Exit                                            │    │
│  │                                                      │    │
│  │  > Select option: _                                 │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  • Direct service calls                                      │
│  • No HTTP overhead                                          │
│  • Command-line friendly                                     │
│  • Great for automation/scripting                            │
│                                                               │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│                      WEB MODE                                 │
│                  dotnet run --web                             │
├──────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Browser DevUI (https://localhost:5001/devui)       │    │
│  │                                                      │    │
│  │  Agent: [architect-assistant ▼]                     │    │
│  │                                                      │    │
│  │  ┌────────────────────────────────────────────┐    │    │
│  │  │ User: What auth patterns do we use?        │    │    │
│  │  │                                             │    │    │
│  │  │ 🔧 query_architecture_docs                  │    │    │
│  │  │                                             │    │    │
│  │  │ Assistant: Based on ADR-012, we use...    │    │    │
│  │  └────────────────────────────────────────────┘    │    │
│  │                                                      │    │
│  │  Message: ___________________________________        │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  • Visual agent interactions                                 │
│  • Tool call transparency                                    │
│  • Multi-agent selection                                     │
│  • Great for debugging/demos                                 │
│  • REST API also available                                   │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

## Data Flow: RAG Query Example

```
 Browser              Agent               Tool                 Service            Database
    │                   │                  │                      │                 │
    │  "What is CQRS?"  │                  │                      │                 │
    ├──────────────────>│                  │                      │                 │
    │                   │                  │                      │                 │
    │                   │ Analyze intent   │                      │                 │
    │                   │ Decide to call   │                      │                 │
    │                   │ query_arch_docs  │                      │                 │
    │                   │                  │                      │                 │
    │                   │  Call tool       │                      │                 │
    │                   ├─────────────────>│                      │                 │
    │                   │                  │                      │                 │
    │                   │                  │ Get service          │                 │
    │                   │                  │ from DI              │                 │
    │                   │                  ├─────────────────────>│                 │
    │                   │                  │                      │                 │
    │                   │                  │                      │ QueryAsync()    │
    │                   │                  │                      │                 │
    │                   │                  │                      │ Generate        │
    │                   │                  │                      │ embedding       │
    │                   │                  │                      ├────────────────>│
    │                   │                  │                      │                 │
    │                   │                  │                      │ Vector search   │
    │                   │                  │                      │<────────────────┤
    │                   │                  │                      │ Results         │
    │                   │                  │                      │                 │
    │                   │                  │  Format results      │                 │
    │                   │                  │<─────────────────────┤                 │
    │                   │                  │                      │                 │
    │                   │  Return results  │                      │                 │
    │                   │<─────────────────┤                      │                 │
    │                   │                  │                      │                 │
    │                   │ Synthesize       │                      │                 │
    │                   │ response with    │                      │                 │
    │                   │ Azure OpenAI     │                      │                 │
    │                   │                  │                      │                 │
    │  Response with    │                  │                      │                 │
    │  doc references   │                  │                      │                 │
    │<──────────────────┤                  │                      │                 │
    │                   │                  │                      │                 │
```
