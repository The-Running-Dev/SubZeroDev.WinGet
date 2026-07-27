using FluentAssertions;

using SubZeroDev.WinGet.Com;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetComContextTests
{
    [Test]
    public async Task InvokeAsync_UsesOneOwnerThread_ForConcurrentCalls()
    {
        using var context = new WinGetComContext();
        var callerThread = Environment.CurrentManagedThreadId;

        var threads = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => context.InvokeAsync(() => Environment.CurrentManagedThreadId)));

        threads.Should().OnlyContain(thread => thread != callerThread);
        threads.Distinct().Should().ContainSingle();
    }

    [Test]
    public async Task InvokeAsync_DoesNotRunExternalContinuationOnOwnerThread()
    {
        using var context = new WinGetComContext();
        var ownerThread = await context.InvokeAsync(() => Environment.CurrentManagedThreadId);
        var task = context.InvokeAsync(() => 42);

        var continuationThread = await task.ContinueWith(
            _ => Environment.CurrentManagedThreadId,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        continuationThread.Should().NotBe(ownerThread);
    }

    [Test]
    public async Task InvokeAsync_AsyncContinuationRemainsOnOwnerThread()
    {
        using var context = new WinGetComContext();

        var threads = await context.InvokeAsync(async () =>
        {
            var before = Environment.CurrentManagedThreadId;
            await Task.Yield();
            return (before, Environment.CurrentManagedThreadId);
        });

        threads.before.Should().Be(threads.Item2);
    }

    [Test]
    public async Task InvokeAsync_CancellationBeforeStart_DoesNotRunWork()
    {
        using var context = new WinGetComContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var ran = false;

        var act = async () => await context.InvokeAsync(() => ran = true, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        ran.Should().BeFalse();
    }

    [Test]
    public async Task RegisterCancellation_PostsCancellationToOwnerThread()
    {
        using var context = new WinGetComContext();
        using var cancellation = new CancellationTokenSource();
        var ownerThread = await context.InvokeAsync(() => Environment.CurrentManagedThreadId);
        var cancelled = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = context.RegisterCancellation(cancellation.Token,
            () => cancelled.TrySetResult(Environment.CurrentManagedThreadId));

        cancellation.Cancel();

        (await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be(ownerThread);
    }

    [Test]
    public async Task InvokeAsync_PropagatesExceptions()
    {
        using var context = new WinGetComContext();

        var act = async () => await context.InvokeAsync(new Func<int>(() => throw new InvalidOperationException("expected")));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("expected");
    }

    [Test]
    public async Task Dispose_DrainsAnInFlightAsyncCallbackBeforeClosingQueue()
    {
        var context = new WinGetComContext();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = context.InvokeAsync(async () =>
        {
            started.TrySetResult();
            await release.Task;
            return 1;
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var dispose = Task.Run(context.Dispose);
        Func<Task> awaitWork = async () => await work;
        await awaitWork.Should().ThrowAsync<OperationCanceledException>();
        release.TrySetResult();
        await dispose.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task Dispose_ConcurrentWithInvoke_CompletesEveryReturnedTask()
    {
        var context = new WinGetComContext();
        var work = Enumerable.Range(0, 32)
            .Select(value => context.InvokeAsync(() => value))
            .ToArray();

        var dispose = Task.Run(context.Dispose);
        await Task.WhenAll(work.Select(task => task.ContinueWith(_ => { }, TaskScheduler.Default)))
            .WaitAsync(TimeSpan.FromSeconds(3));
        await dispose.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task InvokeAsync_RacingDispose_NeverLeavesATaskIncomplete()
    {
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var context = new WinGetComContext();
            var producer = Task.Run(() => Enumerable.Range(0, 32)
                .Select(value => context.InvokeAsync(() => value))
                .ToArray());
            var disposer = Task.Run(context.Dispose);
            var tasks = await producer;

            await Task.WhenAll(tasks.Select(task => task.ContinueWith(_ => { }, TaskScheduler.Default)))
                .WaitAsync(TimeSpan.FromSeconds(3));
            await disposer.WaitAsync(TimeSpan.FromSeconds(3));
        }
    }

    [Test]
    public async Task RegisterCancellation_DisposeStopsFutureCancellationPosts()
    {
        using var context = new WinGetComContext();
        using var cancellation = new CancellationTokenSource();
        var called = 0;
        var registration = context.RegisterCancellation(cancellation.Token, () => Interlocked.Increment(ref called));
        registration.Dispose();
        cancellation.Cancel();
        await Task.Delay(50);

        called.Should().Be(0);
    }

    [Test]
    public async Task Dispose_CancelsAnInFlightRegistrationOnTheOwnerThread()
    {
        var context = new WinGetComContext();
        var ownerThread = await context.InvokeAsync(() => Environment.CurrentManagedThreadId);
        var cancelled = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = context.RegisterCancellation(CancellationToken.None,
            () => cancelled.TrySetResult(Environment.CurrentManagedThreadId));

        await Task.Run(context.Dispose).WaitAsync(TimeSpan.FromSeconds(3));

        (await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3))).Should().Be(ownerThread);
    }

    [Test]
    public async Task Dispose_CancelsBlockedOwnerOperationAndReturns()
    {
        var context = new WinGetComContext();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = context.InvokeAsync(async () =>
        {
            using var registration = context.RegisterCancellation(CancellationToken.None,
                () => operation.TrySetCanceled());
            started.TrySetResult();
            return await operation.Task;
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Run(context.Dispose).WaitAsync(TimeSpan.FromSeconds(3));
        Func<Task> awaitWork = async () => await work;
        await awaitWork.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task Dispose_OnOwnerThread_DoesNotSelfJoin()
    {
        var context = new WinGetComContext();
        var work = context.InvokeAsync(() =>
        {
            context.Dispose();
            return 1;
        });

        Func<Task> awaitWork = async () => await work;
        await awaitWork.Should().ThrowAsync<OperationCanceledException>();
        context.Dispose();
    }
}
