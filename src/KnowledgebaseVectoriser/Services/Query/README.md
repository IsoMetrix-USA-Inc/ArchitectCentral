# Query Optimizer Service

## Overview

The Query Optimizer Service intelligently pre-processes search queries to improve RAG retrieval quality through query expansion, rewriting, classification, and automatic filter suggestion.

## Features

### 🔍 **4 Core Optimization Techniques**

1. **Query Expansion** - Add synonyms and related terms
2. **Query Rewriting** - Transform vague queries into specific searches
3. **Query Classification** - Route queries to optimal search strategies
4. **Filter Suggestion** - Automatically recommend metadata filters

### 🎯 **Query Type Classification**

| Type | Example | Strategy |
|------|---------|----------|
| ADR Lookup | "What is ADR-0015?" | Direct metadata lookup |
| Comparative | "Microservices vs monolith" | Multi-entity fetch |
| How-To | "How to implement CQRS?" | Guidelines-first search |
| Factual | "What is event sourcing?" | Definitions-first search |
| Conceptual | "Authentication patterns" | Full hybrid search |

## Configuration

### Basic Configuration

```csharp
var config = new QueryOptimizationConfig
{
    EnableQueryExpansion = true,
    EnableQueryRewriting = true,
    EnableQueryClassification = true,
    EnableFilterSuggestion = true,
    MaxExpandedTerms = 5,
    UseConversationContext = false
};
```

### Advanced Configuration

```csharp
var config = new QueryOptimizationConfig
{
    EnableQueryExpansion = true,
    MaxExpandedTerms = 10,           // Increase for broader search
    EnableQueryRewriting = true,
    EnableQueryClassification = true,
    EnableFilterSuggestion = true,
    UseConversationContext = true    // Use conversation history
};
```

## Usage

### Optimize Single Query

```csharp
var optimizer = new QueryOptimizerService(logger, config);
var query = "How do we handle auth?";

var optimized = optimizer.OptimizeQuery(query);

Console.WriteLine($"Original: {optimized.OriginalQuery}");
Console.WriteLine($"Processed: {optimized.ProcessedQuery}");
Console.WriteLine($"Type: {optimized.QueryType}");
Console.WriteLine($"Strategy: {optimized.SearchStrategy}");
Console.WriteLine($"Expansions: {optimized.ExpandedTerms.Count}");
Console.WriteLine($"Suggested Filters: {optimized.SuggestedFilters.Count}");
```

### With Conversation Context

```csharp
var conversationContext = "Previous discussion about Azure AD B2C integration";
var optimized = optimizer.OptimizeQuery(
    "How do we implement it?",
    conversationContext
);

// Rewritten query includes context for clarity
Console.WriteLine(optimized.ProcessedQuery);
// Output: "How do we implement the system? (context: Previous discussion about Azure AD B2C integration)"
```

### Batch Optimization

```csharp
var queries = new List<string>
{
    "Best auth practices?",
    "ADR-0015",
    "CQRS vs event sourcing",
    "How to deploy to Azure?"
};

var optimizedQueries = optimizer.OptimizeQueries(queries);

foreach (var opt in optimizedQueries)
{
    Console.WriteLine($"{opt.OriginalQuery} → {opt.QueryType}");
}
```

## Optimization Details

### 1. Query Expansion

**Purpose:** Improve recall by adding synonyms and expanding abbreviations

**Synonym Dictionary** (15 key terms):

| Term | Synonyms |
|------|----------|
| authentication | auth, identity, login, sso, oidc, oauth |
| authorization | authz, permissions, access control, rbac, claims |
| database | db, data store, persistence, repository, data access |
| api | endpoint, web service, rest, http, web api |
| microservice | microservices, service, distributed system, soa |
| cache | caching, redis, in-memory, distributed cache |
| message | messaging, queue, event, pub/sub, bus |
| deployment | deploy, release, cicd, pipeline, devops |
| monitoring | observability, logging, telemetry, metrics, tracing |
| security | secure, encryption, ssl, tls, certificate |
| performance | optimization, latency, throughput, scalability |
| testing | test, unit test, integration test, qa, quality |
| frontend | ui, user interface, spa, client, browser |
| backend | server, api, service layer, business logic |
| error | exception, failure, bug, issue, problem |

**Abbreviation Expansions** (19 common abbreviations):

| Abbreviation | Expansion |
|--------------|-----------|
| auth | authentication |
| authz | authorization |
| db | database |
| api | application programming interface |
| ui | user interface |
| ux | user experience |
| cicd | continuous integration continuous deployment |
| sso | single sign-on |
| oauth | open authorization |
| oidc | openid connect |
| jwt | json web token |
| crud | create read update delete |
| rbac | role-based access control |
| sql | structured query language |
| orm | object relational mapping |
| dto | data transfer object |
| ddd | domain driven design |
| cqrs | command query responsibility segregation |
| tdd | test driven development |

**Example:**

```csharp
// Input
query: "auth with JWT"

// Output
ExpandedTerms: [
    {
        OriginalTerm: "auth",
        ExpandedTerms: ["authentication", "identity", "login", "sso", "oidc"],
        ExpansionType: "synonym"
    },
    {
        OriginalTerm: "JWT",
        ExpandedTerms: ["json web token"],
        ExpansionType: "abbreviation"
    }
]

ProcessedQuery: "auth (authentication) with JWT (json web token)"
```

### 2. Query Rewriting

**Purpose:** Transform vague queries into more specific, searchable terms

**Vague Term Expansions:**

| Vague Term | Expansion |
|------------|-----------|
| stuff | components and patterns |
| things | components and configurations |
| it | the system |
| that | the approach |
| how | implementation approach for |
| errors? | error handling patterns |

**Domain Context Addition:**

```csharp
// Input: Short, vague query
"auth data"

// Output: Domain-qualified query
"auth data architecture patterns"
```

**Examples:**

| Original | Rewritten |
|----------|-----------|
| "How do we handle errors?" | "How do we handle error handling patterns?" |
| "Database stuff" | "Database components and patterns" |
| "Auth" | "Auth in .NET architecture" |
| "That approach" | "The approach" |

### 3. Query Classification

**Purpose:** Route queries to optimal search strategies for better relevance

**Classification Patterns:**

#### ADR Lookup
- **Pattern:** `adr-\d{4}` or `decision-\d{4}`
- **Example:** "What is ADR-0015?"
- **Strategy:** Direct metadata lookup (fast, exact match)

#### Comparative
- **Keywords:** vs, versus, compared to, difference between, choose between, or
- **Example:** "Microservices vs modular monolith"
- **Strategy:** Multi-entity fetch (retrieve both concepts, then contrast)

#### How-To
- **Keywords:** how to, how do, steps to, guide to, implement, setup, configure
- **Example:** "How to implement CQRS in .NET?"
- **Strategy:** Guidelines-first (prioritize how-to content and examples)

#### Factual
- **Keywords:** what is, define, definition, explain, describe
- **Example:** "What is event sourcing?"
- **Strategy:** Definitions-first (prioritize explanatory content)

#### Conceptual (Default)
- **Example:** "Authentication patterns for multi-tenant apps"
- **Strategy:** Full hybrid search (vector + keyword + metadata)

**Classification Logic:**

```
if has_adr_reference(query):
    → DirectLookup
elif is_comparative(query):
    → MultiEntityFetch
elif is_how_to(query):
    → GuidelinesFirst
elif is_factual(query):
    → DefinitionsFirst
else:
    → HybridSearch
```

### 4. Filter Suggestion

**Purpose:** Automatically recommend metadata filters based on query intent

**Suggestion Rules:**

| Query Pattern | Suggested Filter | Reason |
|---------------|------------------|--------|
| "best practice", "recommended" | quality_score >= 0.8 | High quality filter |
| "best practice", "should use" | sentiment = positive | Positive sentiment filter |
| "avoid", "anti-pattern", "wrong" | sentiment = negative | Anti-pattern filter |
| "current", "accepted", "our approach" | status = accepted | Accepted ADRs only |
| "example", "code", "implementation" | has_code_examples = true | Code examples filter |
| "ADR-\d{4}" | doc_type = ADR | ADR documents only |

**Example:**

```csharp
// Input
query: "What are the best practices for authentication?"

// Output
SuggestedFilters: [
    {
        FilterType: "quality_score",
        FilterValue: ">=0.8",
        Reason: "Query requests best practices - filtering for high quality content"
    },
    {
        FilterType: "sentiment",
        FilterValue: "positive",
        Reason: "Query requests best practices - filtering for positive sentiment"
    }
]
```

## Complete Example

### Input Query
```csharp
var query = "What are the best practices for auth in our system?";
```

### Optimization Output

```csharp
OriginalQuery: "What are the best practices for auth in our system?"

ProcessedQuery: "What are the best practices for auth (authentication) in .NET architecture?"

QueryType: Factual

SearchStrategy: DefinitionsFirst

ExpandedTerms: [
    {
        OriginalTerm: "auth",
        ExpandedTerms: ["authentication", "identity", "login", "sso", "oidc"],
        ExpansionType: "synonym"
    }
]

SuggestedFilters: [
    {
        FilterType: "quality_score",
        FilterValue: ">=0.8",
        Reason: "Query requests best practices - filtering for high quality content"
    },
    {
        FilterType: "sentiment",
        FilterValue: "positive",
        Reason: "Query requests best practices - filtering for positive sentiment"
    }
]

KeyEntities: ["auth", "authentication"]

RewriteApplied: true
```

### Using the Optimization

```csharp
// 1. Optimize query
var optimizer = new QueryOptimizerService(logger, config);
var optimized = optimizer.OptimizeQuery(query);

// 2. Build search query with expansions
var searchTerms = new List<string> { optimized.OriginalQuery };
foreach (var expansion in optimized.ExpandedTerms)
{
    searchTerms.AddRange(expansion.ExpandedTerms);
}

// 3. Apply suggested filters
var filters = new Dictionary<string, object>();
foreach (var suggestion in optimized.SuggestedFilters)
{
    filters[suggestion.FilterType] = suggestion.FilterValue;
}

// 4. Execute search with strategy
var results = optimized.SearchStrategy switch
{
    SearchStrategy.DirectLookup => await DirectLookupSearch(optimized.KeyEntities),
    SearchStrategy.DefinitionsFirst => await DefinitionsSearch(searchTerms, filters),
    SearchStrategy.GuidelinesFirst => await GuidelinesSearch(searchTerms, filters),
    SearchStrategy.HybridSearch => await HybridSearch(searchTerms, filters),
    _ => await HybridSearch(searchTerms, filters)
};
```

## Performance

**Typical Performance:**
- **Single query:** ~2-3ms
- **Batch (50 queries):** ~100-150ms
- **Average:** ~2ms per query

**Regex Pre-compilation:**
- All patterns are compiled once at initialization
- Static patterns shared across all service instances
- Zero memory allocations for pattern matching

## Integration with RAG Pipeline

```csharp
// Complete RAG query pipeline with optimization

public async Task<List<SearchResult>> SearchWithOptimization(
    string userQuery,
    string? conversationContext = null)
{
    // Step 1: Optimize query
    var optimized = _queryOptimizer.OptimizeQuery(userQuery, conversationContext);
    
    _logger.LogInformation(
        "Query optimized: {Original} → {Processed} ({Type}, {Strategy})",
        optimized.OriginalQuery,
        optimized.ProcessedQuery,
        optimized.QueryType,
        optimized.SearchStrategy
    );

    // Step 2: Build search terms from expansions
    var searchTerms = GetSearchTerms(optimized);

    // Step 3: Apply suggested filters
    var searchFilters = BuildFilters(optimized.SuggestedFilters);

    // Step 4: Execute hybrid search based on strategy
    var results = await ExecuteSearchStrategy(
        optimized.SearchStrategy,
        searchTerms,
        searchFilters
    );

    // Step 5: Log optimization effectiveness
    _logger.LogInformation(
        "Search completed: {ResultCount} results, Filters: {FilterCount}, Expansions: {ExpansionCount}",
        results.Count,
        searchFilters.Count,
        optimized.ExpandedTerms.Count
    );

    return results;
}
```

## Best Practices

### 1. Use All Optimization Features

```csharp
// Good: Enable all features for maximum quality
var config = new QueryOptimizationConfig
{
    EnableQueryExpansion = true,
    EnableQueryRewriting = true,
    EnableQueryClassification = true,
    EnableFilterSuggestion = true
};
```

### 2. Log Optimization Results

```csharp
// Log what optimizations were applied
_logger.LogInformation(
    "Query: '{Original}' → '{Processed}' | " +
    "Type: {Type}, Expansions: {ExpCount}, Filters: {FilterCount}",
    optimized.OriginalQuery,
    optimized.ProcessedQuery,
    optimized.QueryType,
    optimized.ExpandedTerms.Count,
    optimized.SuggestedFilters.Count
);
```

### 3. Track Optimization Impact

```csharp
// Compare search results with and without optimization
var baselineResults = await Search(originalQuery);
var optimizedResults = await Search(optimizedQuery);

var improvement = optimizedResults.Count - baselineResults.Count;
_logger.LogInformation(
    "Optimization impact: {Improvement} additional results",
    improvement
);
```

### 4. Use Conversation Context for Multi-Turn

```csharp
// Store conversation history
var conversationContext = string.Join(" | ", previousQueries);

// Use context for follow-up queries
var optimized = optimizer.OptimizeQuery("How do we implement that?", conversationContext);
```

## Troubleshooting

### Issue: Over-Expansion

**Symptom:** Too many synonyms, results become too broad

**Solution:**
```csharp
config.MaxExpandedTerms = 3;  // Reduce from default 5
```

### Issue: Incorrect Classification

**Symptom:** Query classified as wrong type

**Solution:**
```csharp
// Add custom patterns or adjust existing regex
// Or disable classification and use strategy directly
config.EnableQueryClassification = false;
```

### Issue: Filters Too Restrictive

**Symptom:** Filter suggestions reduce results to zero

**Solution:**
```csharp
// Treat suggestions as hints, not requirements
// Apply filters progressively
var results = await SearchWithFilters(query, primaryFilters);
if (results.Count == 0)
{
    results = await SearchWithFilters(query, secondaryFilters);
}
```

## Extension Points

### Add Custom Synonyms

```csharp
// Extend the synonym dictionary
SynonymDictionary["kubernetes"] = new() { "k8s", "container orchestration", "cluster" };
SynonymDictionary["react"] = new() { "reactjs", "react.js", "frontend framework" };
```

### Add Custom Abbreviations

```csharp
// Extend the abbreviation dictionary
AbbreviationExpansions["k8s"] = "kubernetes";
AbbreviationExpansions["ts"] = "typescript";
```

### Add Custom Classification Rules

```csharp
// Add new query type pattern
private static readonly Regex TroubleshootingRegex = new(
    @"\b(?:fix|troubleshoot|debug|solve|resolve|issue with)\b",
    RegexOptions.IgnoreCase | RegexOptions.Compiled
);

// Update classification logic
if (TroubleshootingRegex.IsMatch(queryLower))
    return QueryType.Troubleshooting;
```

## References

- [RAG Research Findings](../../../files/rag-research-findings.md) - Query optimization patterns
- [Enhanced Schema Design](../../../files/enhanced-schema-design.md) - Filter metadata schema
- [Metadata Enricher Service](../Metadata/README.md) - Metadata used in filters

---

**Version:** 1.0.0  
**Last Updated:** 2026-05-11  
**Status:** Production Ready
