using System.Text.RegularExpressions;

namespace ArchitectCentral.AgentStudio.Services.Query
{
    /// <summary>
    /// Configuration for query optimization
    /// </summary>
    public class QueryOptimizationConfig
    {
        /// <summary>
        /// Enable query expansion with synonyms
        /// </summary>
        public bool EnableQueryExpansion { get; set; } = true;

        /// <summary>
        /// Enable query rewriting for clarity
        /// </summary>
        public bool EnableQueryRewriting { get; set; } = true;

        /// <summary>
        /// Enable query classification and routing
        /// </summary>
        public bool EnableQueryClassification { get; set; } = true;

        /// <summary>
        /// Enable automatic filter suggestion
        /// </summary>
        public bool EnableFilterSuggestion { get; set; } = true;

        /// <summary>
        /// Maximum number of expanded terms per query
        /// </summary>
        public int MaxExpandedTerms { get; set; } = 5;

        /// <summary>
        /// Enable conversation history context
        /// </summary>
        public bool UseConversationContext { get; set; }
    }

    /// <summary>
    /// Service for optimizing search queries through expansion, rewriting, and classification
    /// </summary>
    public class QueryOptimizerService
    {
        private readonly ILogger<QueryOptimizerService> _logger;
        private readonly QueryOptimizationConfig _config;

        // Technical terminology synonym dictionary
        private static readonly Dictionary<string, List<string>> SynonymDictionary = new()
        {
            ["authentication"] = new() { "auth", "identity", "login", "sso", "oidc", "oauth" },
            ["authorization"] = new() { "authz", "permissions", "access control", "rbac", "claims" },
            ["database"] = new() { "db", "data store", "persistence", "repository", "data access" },
            ["api"] = new() { "endpoint", "web service", "rest", "http", "web api" },
            ["microservice"] = new() { "microservices", "service", "distributed system", "soa" },
            ["cache"] = new() { "caching", "redis", "in-memory", "distributed cache" },
            ["message"] = new() { "messaging", "queue", "event", "pub/sub", "bus" },
            ["deployment"] = new() { "deploy", "release", "cicd", "pipeline", "devops" },
            ["monitoring"] = new() { "observability", "logging", "telemetry", "metrics", "tracing" },
            ["security"] = new() { "secure", "encryption", "ssl", "tls", "certificate" },
            ["performance"] = new() { "optimization", "latency", "throughput", "scalability" },
            ["testing"] = new() { "test", "unit test", "integration test", "qa", "quality" },
            ["frontend"] = new() { "ui", "user interface", "spa", "client", "browser" },
            ["backend"] = new() { "server", "api", "service layer", "business logic" },
            ["error"] = new() { "exception", "failure", "bug", "issue", "problem" }
        };

        // Common abbreviation expansions
        private static readonly Dictionary<string, string> AbbreviationExpansions = new()
        {
            ["auth"] = "authentication",
            ["authz"] = "authorization",
            ["db"] = "database",
            ["api"] = "application programming interface",
            ["ui"] = "user interface",
            ["ux"] = "user experience",
            ["cicd"] = "continuous integration continuous deployment",
            ["sso"] = "single sign-on",
            ["oauth"] = "open authorization",
            ["oidc"] = "openid connect",
            ["jwt"] = "json web token",
            ["crud"] = "create read update delete",
            ["rbac"] = "role-based access control",
            ["sql"] = "structured query language",
            ["orm"] = "object relational mapping",
            ["dto"] = "data transfer object",
            ["ddd"] = "domain driven design",
            ["cqrs"] = "command query responsibility segregation",
            ["tdd"] = "test driven development",
            ["bdd"] = "behavior driven development"
        };

        // Query type patterns
        private static readonly Regex AdrReferenceRegex = new(
            @"adr[-\s]?(\d{4})|decision[-\s]?(\d{4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex ComparativeRegex = new(
            @"\b(?:vs|versus|compared to|difference between|choose between|or)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex HowToRegex = new(
            @"\b(?:how to|how do|how can|how should|steps to|guide to|tutorial|implement|setup|configure)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex FactualRegex = new(
            @"\b(?:what is|define|definition|explain|describe)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        // Filter suggestion patterns
        private static readonly Regex BestPracticeRegex = new(
            @"\b(?:best practice|recommended|preferred|should use|correct way)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex AntiPatternRegex = new(
            @"\b(?:avoid|don't|wrong|bad practice|anti[- ]?pattern|mistake|pitfall)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex CurrentApproachRegex = new(
            @"\b(?:current|accepted|approved|our approach|what we use|standard)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex ExamplesRegex = new(
            @"\b(?:example|sample|demo|code|implementation|usage)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        public QueryOptimizerService(
            ILogger<QueryOptimizerService> logger,
            QueryOptimizationConfig? config = null)
        {
            _logger = logger;
            _config = config ?? new QueryOptimizationConfig();
        }

        /// <summary>
        /// Optimize a query through expansion, rewriting, and classification
        /// </summary>
        public OptimizedQuery OptimizeQuery(string originalQuery, string? conversationContext = null)
        {
            _logger.LogDebug("Optimizing query: {Query}", originalQuery);

            var optimized = new OptimizedQuery
            {
                OriginalQuery = originalQuery,
                ProcessedQuery = originalQuery
            };

            // Step 1: Query Classification
            if (_config.EnableQueryClassification)
            {
                optimized.QueryType = ClassifyQuery(originalQuery);
                optimized.SearchStrategy = DetermineSearchStrategy(optimized.QueryType);
            }

            // Step 2: Query Expansion
            if (_config.EnableQueryExpansion)
            {
                optimized.ExpandedTerms = ExpandQuery(originalQuery);
                if (optimized.ExpandedTerms.Count != 0)
                {
                    optimized.ProcessedQuery = ApplyExpansions(originalQuery, optimized.ExpandedTerms);
                }
            }

            // Step 3: Query Rewriting
            if (_config.EnableQueryRewriting)
            {
                optimized.ProcessedQuery = RewriteQuery(
                    optimized.ProcessedQuery,
                    conversationContext
                );
                optimized.RewriteApplied = optimized.ProcessedQuery != originalQuery;
            }

            // Step 4: Filter Suggestions
            if (_config.EnableFilterSuggestion)
            {
                optimized.SuggestedFilters = SuggestFilters(originalQuery);
            }

            // Extract key entities and concepts
            optimized.KeyEntities = ExtractKeyEntities(originalQuery);

            _logger.LogInformation(
                "Query optimized: Type={Type}, Strategy={Strategy}, Expansions={Expansions}, " +
                "Filters={Filters}, Rewrite={Rewrite}",
                optimized.QueryType,
                optimized.SearchStrategy,
                optimized.ExpandedTerms.Count,
                optimized.SuggestedFilters.Count,
                optimized.RewriteApplied
            );

            return optimized;
        }

        /// <summary>
        /// Classify the query type
        /// </summary>
        private static QueryType ClassifyQuery(string query)
        {
            var queryLower = query.ToLowerInvariant();

            // Check for ADR reference
            if (AdrReferenceRegex.IsMatch(query))
                return QueryType.AdrLookup;

            // Check for comparative
            if (ComparativeRegex.IsMatch(queryLower))
                return QueryType.Comparative;

            // Check for how-to
            if (HowToRegex.IsMatch(queryLower))
                return QueryType.HowTo;

            // Check for factual
            if (FactualRegex.IsMatch(queryLower))
                return QueryType.Factual;

            // Default to conceptual
            return QueryType.Conceptual;
        }

        /// <summary>
        /// Determine optimal search strategy based on query type
        /// </summary>
        private static SearchStrategy DetermineSearchStrategy(QueryType queryType)
        {
            return queryType switch
            {
                QueryType.AdrLookup => SearchStrategy.DirectLookup,
                QueryType.Comparative => SearchStrategy.MultiEntityFetch,
                QueryType.HowTo => SearchStrategy.GuidelinesFirst,
                QueryType.Factual => SearchStrategy.DefinitionsFirst,
                QueryType.Conceptual => SearchStrategy.HybridSearch,
                _ => SearchStrategy.HybridSearch
            };
        }

        /// <summary>
        /// Expand query with synonyms and related terms
        /// </summary>
        private List<QueryExpansion> ExpandQuery(string query)
        {
            var expansions = new List<QueryExpansion>();
            var queryLower = query.ToLowerInvariant();

            // Find matching terms in synonym dictionary
            foreach (var (term, synonyms) in SynonymDictionary)
            {
                if (queryLower.Contains(term))
                {
                    var selectedSynonyms = synonyms
                        .Take(_config.MaxExpandedTerms)
                        .ToList();

                    expansions.Add(new QueryExpansion
                    {
                        OriginalTerm = term,
                        ExpandedTerms = selectedSynonyms,
                        ExpansionType = "synonym"
                    });
                }
            }

            // Expand abbreviations
            var words = query.Split([' ', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                var wordLower = word.ToLowerInvariant().Trim();
                if (AbbreviationExpansions.TryGetValue(wordLower, out var expansion))
                {
                    expansions.Add(new QueryExpansion
                    {
                        OriginalTerm = word,
                        ExpandedTerms = new List<string> { expansion },
                        ExpansionType = "abbreviation"
                    });
                }
            }

            return expansions;
        }

        /// <summary>
        /// Apply expansions to query
        /// </summary>
        private static string ApplyExpansions(string query, List<QueryExpansion> expansions)
        {
            var processed = query;

            foreach (var expansion in expansions)
            {
                // For abbreviations, append expansion in parentheses
                if (expansion.ExpansionType == "abbreviation")
                {
                    var pattern = $@"\b{Regex.Escape(expansion.OriginalTerm)}\b";
                    var replacement = $"{expansion.OriginalTerm} ({expansion.ExpandedTerms[0]})";
                    processed = Regex.Replace(processed, pattern, replacement, RegexOptions.IgnoreCase);
                }
                // For synonyms, we'll use them in search but not modify the display query
            }

            return processed;
        }

        /// <summary>
        /// Rewrite query for clarity and specificity
        /// </summary>
        private string RewriteQuery(string query, string? conversationContext)
        {
            var rewritten = query;

            // Expand common vague terms
            rewritten = ExpandVagueTerms(rewritten);

            // Add domain context for ambiguous queries
            rewritten = AddDomainContext(rewritten);

            // Add conversation context if available
            if (_config.UseConversationContext && !string.IsNullOrWhiteSpace(conversationContext))
            {
                rewritten = $"{rewritten} (context: {conversationContext})";
            }

            return rewritten;
        }

        /// <summary>
        /// Expand vague terms with more specific alternatives
        /// </summary>
        private static string ExpandVagueTerms(string query)
        {
            var expansions = new Dictionary<string, string>
            {
                [@"\bstuff\b"] = "components and patterns",
                [@"\bthings\b"] = "components and configurations",
                [@"\bit\b"] = "the system",
                [@"\bthat\b"] = "the approach",
                [@"\bhow\b"] = "implementation approach for",
                [@"\berrors?\b"] = "error handling patterns"
            };

            var rewritten = query;
            foreach (var (pattern, replacement) in expansions)
            {
                rewritten = Regex.Replace(rewritten, pattern, replacement, RegexOptions.IgnoreCase);
            }

            return rewritten;
        }

        /// <summary>
        /// Add domain-specific context to query
        /// </summary>
        private static string AddDomainContext(string query)
        {
            var queryLower = query.ToLowerInvariant();

            // If query is very short and vague, add architectural context
            if (query.Split(' ').Length <= 3)
            {
                // Check if it needs domain qualification
                if (queryLower.Contains("auth") && !queryLower.Contains("architecture"))
                {
                    return $"{query} in .NET architecture";
                }
                if (queryLower.Contains("data") && !queryLower.Contains("architecture"))
                {
                    return $"{query} architecture patterns";
                }
            }

            return query;
        }

        /// <summary>
        /// Suggest metadata filters based on query intent
        /// </summary>
        private static List<FilterSuggestion> SuggestFilters(string query)
        {
            var suggestions = new List<FilterSuggestion>();
            var queryLower = query.ToLowerInvariant();

            // Suggest quality filter for best practices
            if (BestPracticeRegex.IsMatch(queryLower))
            {
                suggestions.Add(new FilterSuggestion
                {
                    FilterType = "quality_score",
                    FilterValue = ">=0.8",
                    Reason = "Query requests best practices - filtering for high quality content"
                });

                suggestions.Add(new FilterSuggestion
                {
                    FilterType = "sentiment",
                    FilterValue = "positive",
                    Reason = "Query requests best practices - filtering for positive sentiment"
                });
            }

            // Suggest sentiment filter for anti-patterns
            if (AntiPatternRegex.IsMatch(queryLower))
            {
                suggestions.Add(new FilterSuggestion
                {
                    FilterType = "sentiment",
                    FilterValue = "negative",
                    Reason = "Query requests anti-patterns - filtering for negative sentiment"
                });
            }

            // Suggest status filter for current approach
            if (CurrentApproachRegex.IsMatch(queryLower))
            {
                suggestions.Add(new FilterSuggestion
                {
                    FilterType = "status",
                    FilterValue = "accepted",
                    Reason = "Query requests current approach - filtering for accepted ADRs"
                });
            }

            // Suggest code examples filter
            if (ExamplesRegex.IsMatch(queryLower))
            {
                suggestions.Add(new FilterSuggestion
                {
                    FilterType = "has_code_examples",
                    FilterValue = "true",
                    Reason = "Query requests examples - filtering for chunks with code"
                });
            }

            // Suggest doc type for ADR queries
            if (AdrReferenceRegex.IsMatch(query))
            {
                suggestions.Add(new FilterSuggestion
                {
                    FilterType = "doc_type",
                    FilterValue = "ADR",
                    Reason = "Query references ADR - filtering for ADR documents"
                });
            }

            return suggestions;
        }

        /// <summary>
        /// Extract key entities and concepts from query
        /// </summary>
        private static List<string> ExtractKeyEntities(string query)
        {
            var entities = new HashSet<string>();

            // Extract ADR numbers
            var adrMatches = AdrReferenceRegex.Matches(query);
            foreach (Match match in adrMatches)
            {
                var number = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                entities.Add($"ADR-{number}");
            }

            // Extract capitalized terms (likely proper nouns)
            var capitalizedRegex = new Regex(@"\b[A-Z][A-Za-z0-9]*(?:\s+[A-Z][A-Za-z0-9]*)*\b");
            var capitalizedMatches = capitalizedRegex.Matches(query);
            foreach (Match match in capitalizedMatches)
            {
                if (match.Value.Length > 2) // Skip short acronyms
                {
                    entities.Add(match.Value);
                }
            }

            // Extract technical terms from synonym dictionary
            var queryLower = query.ToLowerInvariant();
            foreach (var term in SynonymDictionary.Keys)
            {
                if (queryLower.Contains(term))
                {
                    entities.Add(term);
                }
            }

            return entities.OrderBy(e => e).ToList();
        }

        /// <summary>
        /// Batch optimize multiple queries
        /// </summary>
        public List<OptimizedQuery> OptimizeQueries(List<string> queries, string? conversationContext = null)
        {
            _logger.LogInformation("Batch optimizing {Count} queries", queries.Count);

            var results = new List<OptimizedQuery>();
            foreach (var query in queries)
            {
                results.Add(OptimizeQuery(query, conversationContext));
            }

            return results;
        }
    }

    /// <summary>
    /// Optimized query with expansions, rewrites, and metadata
    /// </summary>
    public class OptimizedQuery
    {
        public string OriginalQuery { get; set; } = string.Empty;
        public string ProcessedQuery { get; set; } = string.Empty;
        public QueryType QueryType { get; set; }
        public SearchStrategy SearchStrategy { get; set; }
        public List<QueryExpansion> ExpandedTerms { get; set; } = new();
        public List<FilterSuggestion> SuggestedFilters { get; set; } = new();
        public List<string> KeyEntities { get; set; } = new();
        public bool RewriteApplied { get; set; }
    }

    /// <summary>
    /// Query expansion with synonyms or abbreviations
    /// </summary>
    public class QueryExpansion
    {
        public string OriginalTerm { get; set; } = string.Empty;
        public List<string> ExpandedTerms { get; set; } = new();
        public string ExpansionType { get; set; } = string.Empty; // "synonym" or "abbreviation"
    }

    /// <summary>
    /// Suggested filter for the query
    /// </summary>
    public class FilterSuggestion
    {
        public string FilterType { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Query type classification
    /// </summary>
    public enum QueryType
    {
        AdrLookup,       // "What is ADR-0015?"
        Comparative,     // "Microservices vs monolith"
        HowTo,          // "How to implement CQRS?"
        Factual,        // "What is event sourcing?"
        Conceptual      // "Authentication patterns"
    }

    /// <summary>
    /// Search strategy based on query type
    /// </summary>
    public enum SearchStrategy
    {
        DirectLookup,       // Direct metadata lookup (ADRs, specific IDs)
        MultiEntityFetch,   // Fetch multiple entities for comparison
        GuidelinesFirst,    // Prioritize guidelines and how-to content
        DefinitionsFirst,   // Prioritize definitions and explanations
        HybridSearch       // Full hybrid search (vector + keyword + metadata)
    }
}
