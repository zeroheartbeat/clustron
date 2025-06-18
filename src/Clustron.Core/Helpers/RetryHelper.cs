// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Microsoft.Extensions.Logging;

namespace Clustron.Core.Helpers;
public static class RetryHelper
{
    public static async Task<T?> RetryAsync<T>(Func<Task<T>> action, int maxAttempts, int backoffMillis, ILogger? logger = null)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                if (attempt == maxAttempts)
                    throw;

                logger?.LogWarning("Retry {Attempt} failed: {Message}", attempt, ex.Message);
                await Task.Delay(backoffMillis * attempt); // Exponential backoff
            }
        }

        return default;
    }

    public static async Task RetryAsync(Func<Task> action, int maxAttempts, int backoffMillis, ILogger? logger = null)
    {
        await RetryAsync(async () => { await action(); return true; }, maxAttempts, backoffMillis, logger);
    }
}

