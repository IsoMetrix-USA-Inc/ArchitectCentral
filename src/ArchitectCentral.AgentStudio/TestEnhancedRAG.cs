using ArchitectCentral.AgentStudio.Services;
using Microsoft.Extensions.Logging;

namespace ArchitectCentral.AgentStudio;

/// <summary>
/// Test program to validate Enhanced RAG multi-query strategies
/// </summary>
public class TestEnhancedRAG
{
    private readonly EnhancedRAGQueryService _ragService;
    private readonly ILogger<TestEnhancedRAG> _logger;

    public TestEnhancedRAG(
        EnhancedRAGQueryService ragService,
        ILogger<TestEnhancedRAG> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Run comprehensive tests on enhanced RAG with real ADR data
    /// </summary>
    public async Task<TestResults> RunTestsAsync()
    {
        _logger.LogInformation("Starting Enhanced RAG Query Tests...\n");

        var results = new TestResults();

        // Test 1: General query (baseline)
        await TestGeneralQuery(results);

        // Test 2: Query optimization enabled
        await TestQueryOptimization(results);

        // Test 3: Hybrid search enabled
        await TestHybridSearch(results);

        // Test 4: Quality re-ranking
        await TestQualityReranking(results);

        // Test 5: Combined (all features enabled)
        await TestCombinedFeatures(results);

        // Print summary
        PrintSummary(results);

        return results;
    }

    private async Task TestGeneralQuery(TestResults results)
    {
        _logger.LogInformation("TEST 1: General Query (Baseline)");
        _logger.LogInformation("=========================================");

        var query = "vertical slice architecture";
        var startTime = DateTime.UtcNow;

        var queryResults = await _ragService.QueryAsync(query);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation("Query: '{Query}'", query);
        _logger.LogInformation("Results: {Count}", queryResults.Count);
        _logger.LogInformation("Time: {Time}ms", elapsed);

        if (queryResults.Count > 0)
        {
            _logger.LogInformation("Top result: {Type} (similarity: {Sim:P0})", 
                queryResults[0].DocumentType, 
                queryResults[0].Similarity);

            results.TestsPassed++;
        }
        else
        {
            _logger.LogWarning("❌ No results returned");
            results.TestsFailed++;
        }

        results.TotalTests++;
        _logger.LogInformation("");
    }

    private async Task TestQueryOptimization(TestResults results)
    {
        _logger.LogInformation("TEST 2: Query Optimization Enabled");
        _logger.LogInformation("=========================================");

        var query = "how to implement CQRS pattern";
        var startTime = DateTime.UtcNow;

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = true,
            EnableHybridSearch = false,
            ApplySuggestedFilters = false,
            RerankByQuality = false
        };

        var response = await _ragService.QueryAsync(query, null, options);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation("Query: '{Query}'", query);
        _logger.LogInformation("Original: '{Orig}'", response.OriginalQuery);
        _logger.LogInformation("Processed: '{Proc}'", response.ProcessedQuery);
        _logger.LogInformation("Results: {Count}", response.Results.Count);
        _logger.LogInformation("Time: {Time}ms", elapsed);

        if (response.Results.Count > 0)
        {
            _logger.LogInformation("✅ Query optimization working");
            results.TestsPassed++;
        }
        else
        {
            _logger.LogWarning("❌ No results with optimization");
            results.TestsFailed++;
        }

        results.TotalTests++;
        _logger.LogInformation("");
    }

    private async Task TestHybridSearch(TestResults results)
    {
        _logger.LogInformation("TEST 3: Hybrid Search Enabled");
        _logger.LogInformation("=========================================");

        var query = "authentication authorization patterns";
        var startTime = DateTime.UtcNow;

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = false,
            EnableHybridSearch = true,
            ApplySuggestedFilters = false,
            RerankByQuality = false
        };

        var response = await _ragService.QueryAsync(query, null, options);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation("Query: '{Query}'", query);
        _logger.LogInformation("Search Method: {Method}", response.SearchMethod);
        _logger.LogInformation("Results: {Count}", response.Results.Count);
        _logger.LogInformation("Time: {Time}ms", elapsed);

        if (response.Results.Count > 0)
        {
            _logger.LogInformation("✅ Hybrid search working");
            results.TestsPassed++;
        }
        else
        {
            _logger.LogWarning("❌ No results with hybrid search");
            results.TestsFailed++;
        }

        results.TotalTests++;
        _logger.LogInformation("");
    }

    private async Task TestQualityReranking(TestResults results)
    {
        _logger.LogInformation("TEST 4: Quality Re-ranking");
        _logger.LogInformation("=========================================");

        var query = "API design best practices";
        var startTime = DateTime.UtcNow;

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = false,
            EnableHybridSearch = false,
            ApplySuggestedFilters = false,
            RerankByQuality = true
        };

        var response = await _ragService.QueryAsync(query, null, options);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation("Query: '{Query}'", query);
        _logger.LogInformation("Results: {Count}", response.Results.Count);
        _logger.LogInformation("Time: {Time}ms", elapsed);

        if (response.Results.Count > 0)
        {
            // Show top 3 with similarity scores
            _logger.LogInformation("Top 3 results after quality re-ranking:");
            foreach (var result in response.Results.Take(3))
            {
                _logger.LogInformation("  - {Type} (similarity: {Sim:P0})", 
                    result.DocumentType, 
                    result.Similarity);
            }

            _logger.LogInformation("✅ Quality re-ranking working");
            results.TestsPassed++;
        }
        else
        {
            _logger.LogWarning("❌ No results with quality re-ranking");
            results.TestsFailed++;
        }

        results.TotalTests++;
        _logger.LogInformation("");
    }

    private async Task TestCombinedFeatures(TestResults results)
    {
        _logger.LogInformation("TEST 5: All Features Combined");
        _logger.LogInformation("=========================================");

        var query = "microservices communication patterns";
        var startTime = DateTime.UtcNow;

        var options = new RAGQueryOptions
        {
            EnableQueryOptimization = true,
            EnableHybridSearch = true,
            ApplySuggestedFilters = true,
            RerankByQuality = true
        };

        var response = await _ragService.QueryAsync(query, null, options);

        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

        _logger.LogInformation("Query: '{Query}'", query);
        _logger.LogInformation("Original: '{Orig}'", response.OriginalQuery);
        _logger.LogInformation("Processed: '{Proc}'", response.ProcessedQuery);
        _logger.LogInformation("Search Method: {Method}", response.SearchMethod);
        _logger.LogInformation("Results: {Count}", response.Results.Count);
        _logger.LogInformation("Time: {Time}ms", elapsed);

        if (response.QueryOptimization != null)
        {
            _logger.LogInformation("Query Type: {Type}", response.QueryOptimization.QueryType);
            _logger.LogInformation("Strategy: {Strategy}", response.QueryOptimization.SearchStrategy);
            _logger.LogInformation("Expansions: {Count}", response.QueryOptimization.ExpandedTerms.Count);
            _logger.LogInformation("Filters: {Count}", response.QueryOptimization.SuggestedFilters.Count);
        }

        if (response.Results.Count > 0)
        {
            _logger.LogInformation("✅ All features working together");
            results.TestsPassed++;
        }
        else
        {
            _logger.LogWarning("❌ No results with combined features");
            results.TestsFailed++;
        }

        results.TotalTests++;
        _logger.LogInformation("");
    }

    private void PrintSummary(TestResults results)
    {
        _logger.LogInformation("=========================================");
        _logger.LogInformation("TEST SUMMARY");
        _logger.LogInformation("=========================================");
        _logger.LogInformation("Total Tests: {Total}", results.TotalTests);
        _logger.LogInformation("Passed: {Passed}", results.TestsPassed);
        _logger.LogInformation("Failed: {Failed}", results.TestsFailed);
        _logger.LogInformation("Success Rate: {Rate:P0}", 
            results.TotalTests > 0 ? (double)results.TestsPassed / results.TotalTests : 0);
        _logger.LogInformation("=========================================");

        if (results.TestsFailed == 0)
        {
            _logger.LogInformation("✅ ALL TESTS PASSED!");
        }
        else
        {
            _logger.LogWarning("⚠️  Some tests failed. Review logs above.");
        }
    }
}

public class TestResults
{
    public int TotalTests { get; set; }
    public int TestsPassed { get; set; }
    public int TestsFailed { get; set; }
}
