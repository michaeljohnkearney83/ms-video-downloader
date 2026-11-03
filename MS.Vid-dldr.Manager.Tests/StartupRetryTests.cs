using MS.Vid_dldr.Manager.Helpers;

namespace MS.Vid_dldr.Manager.Tests;

public class StartupRetryTests
{
    [Fact]
    public async Task RunAsync_SucceedsFirstTime_RunsOnceWithoutRetry()
    {
        var calls = 0;
        var retries = 0;

        await StartupRetry.RunAsync(_ => { calls++; return Task.CompletedTask; }, 3, TimeSpan.Zero, (_, _) => retries++);

        Assert.Equal(1, calls);
        Assert.Equal(0, retries);
    }

    [Fact]
    public async Task RunAsync_FailsThenSucceeds_RetriesUntilSuccess()
    {
        var calls = 0;
        var retryAttempts = new List<int>();

        await StartupRetry.RunAsync(_ =>
        {
            calls++;
            if (calls < 3) throw new InvalidOperationException("db not ready");
            return Task.CompletedTask;
        }, 5, TimeSpan.Zero, (attempt, _) => retryAttempts.Add(attempt));

        Assert.Equal(3, calls);
        Assert.Equal(new[] { 1, 2 }, retryAttempts);
    }

    [Fact]
    public async Task RunAsync_AlwaysFails_RethrowsLastExceptionAfterMaxAttempts()
    {
        var calls = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartupRetry.RunAsync(_ => { calls++; throw new InvalidOperationException($"fail {calls}"); }, 3, TimeSpan.Zero));

        Assert.Equal(3, calls);
        Assert.Equal("fail 3", ex.Message);
    }

    [Fact]
    public async Task RunAsync_ActionThrowsOperationCanceled_DoesNotRetry()
    {
        var calls = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            StartupRetry.RunAsync(_ => { calls++; throw new OperationCanceledException(); }, 5, TimeSpan.Zero));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_CancelledDuringDelay_StopsRetrying()
    {
        using var cts = new CancellationTokenSource();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StartupRetry.RunAsync(_ => { calls++; throw new InvalidOperationException(); }, 5, TimeSpan.FromMinutes(1),
                (_, _) => cts.Cancel(), cts.Token));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_AlreadyCancelled_DoesNotRunAction()
    {
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StartupRetry.RunAsync(_ => { calls++; return Task.CompletedTask; }, 3, TimeSpan.Zero, null, new CancellationToken(true)));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RunAsync_InvalidMaxAttempts_Throws(int maxAttempts)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            StartupRetry.RunAsync(_ => Task.CompletedTask, maxAttempts, TimeSpan.Zero));
    }

    [Fact]
    public async Task RunAsync_NegativeDelay_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            StartupRetry.RunAsync(_ => Task.CompletedTask, 1, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task RunAsync_NullAction_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => StartupRetry.RunAsync(null!, 1, TimeSpan.Zero));
    }
}
