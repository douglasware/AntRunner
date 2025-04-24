using Microsoft.DurableTask;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AntRunnerFunctions
{
    /// <summary>
    /// Base retry policy excluding a bunch of typically not retryable exception types.
    /// Probably requires tuning...
    /// </summary>
    internal class RetryPolicy
    {
        static TaskOptions? _retryPolicy;
        public static TaskOptions Get()
        {
            _retryPolicy = _retryPolicy ?? TaskOptions.FromRetryHandler(async retryContext =>
            {
                // List of specific exception types to exclude
                string[] excludedExceptions = {
                    typeof(Exception).ToString(),
                    typeof(ApplicationException).ToString(),
                    typeof(ArgumentNullException).ToString(),
                    typeof(InvalidOperationException).ToString(),
                    typeof(NotSupportedException).ToString(),
                    typeof(ArgumentOutOfRangeException).ToString(),
                    typeof(OperationCanceledException).ToString(),
                    typeof(JsonException).ToString(),
                    typeof(ValidationException).ToString(),
                    typeof(NullReferenceException).ToString()
                };

                foreach (var exceptionType in excludedExceptions)
                {
                    if (!excludedExceptions.Contains(retryContext.LastFailure.ErrorType))
                    {
                        //TODO: Retry timespan config and distinct behaviors for throttling
                        await retryContext.OrchestrationContext.CreateTimer(TimeSpan.FromSeconds(15), retryContext.CancellationToken);
                        return true;
                    }
                    return false;
                }

                //TODO: Make situational behaviors for distinct scenarios like throttling
                // Quit after 3 attempts
                return retryContext.LastAttemptNumber < 3;
            });
            return _retryPolicy;
        }
    }
}
