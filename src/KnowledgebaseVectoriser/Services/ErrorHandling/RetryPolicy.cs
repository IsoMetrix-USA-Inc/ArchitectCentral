using Microsoft.Extensions.Logging;

namespace KnowledgebaseVectoriser.Services.ErrorHandling;

public class RetryPolicy
{
    private readonly ILogger _logger;
    
    public RetryPolicy(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        int maxRetries = 3,
        int delaySeconds = 2,
        bool exponentialBackoff = true)
    {
        var attempt = 0;
        
        while (true)
        {
            try
            {
                attempt++;
                return await operation();
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientError(ex))
            {
                var delay = exponentialBackoff 
                    ? delaySeconds * (int)Math.Pow(2, attempt - 1) 
                    : delaySeconds;
                
                _logger.LogWarning(
                    "Transient error in {Operation} (attempt {Attempt}/{MaxRetries}): {Error}. Retrying in {Delay}s...",
                    operationName, attempt, maxRetries, ex.Message, delay);
                
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error in {Operation} after {Attempt} attempt(s): {Error}",
                    operationName, attempt, ex.Message);
                throw;
            }
        }
    }

    private static bool IsTransientError(Exception ex)
    {
        // Network-related transient errors
        if (ex is HttpRequestException || 
            ex is TaskCanceledException ||
            ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("429") || // Rate limit
            ex.Message.Contains("503") || // Service unavailable
            ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Database transient errors
        if (ex.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
