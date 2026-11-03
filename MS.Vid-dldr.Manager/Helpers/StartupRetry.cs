namespace MS.Vid_dldr.Manager.Helpers;

/// <summary>
/// Retries a startup action (e.g. database migration) that may fail while dependencies are still starting.
/// </summary>
public static class StartupRetry
{
    /// <summary>
    /// Runs <paramref name="action"/> up to <paramref name="maxAttempts"/> times, waiting <paramref name="delay"/>
    /// between attempts. Any exception except <see cref="OperationCanceledException"/> triggers a retry;
    /// the last exception is rethrown when all attempts fail.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="maxAttempts">Total number of attempts (must be at least 1).</param>
    /// <param name="delay">Wait time between attempts (must not be negative).</param>
    /// <param name="onRetry">Optional callback invoked with the attempt number and exception before each retry.</param>
    /// <param name="cancellationToken">Cancels the action and the wait between attempts.</param>
    public static async Task RunAsync(
        Func<CancellationToken, Task> action,
        int maxAttempts,
        TimeSpan delay,
        Action<int, Exception>? onRetry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Must be at least 1.");
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay), "Must not be negative.");

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await action(cancellationToken);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < maxAttempts)
            {
                onRetry?.Invoke(attempt, ex);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
