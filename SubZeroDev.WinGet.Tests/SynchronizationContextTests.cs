using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class SynchronizationContextTests
{
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(3);

    [Test]
    public void Search_CompletesWithoutPumpingCallerSynchronizationContext()
    {
        var client = new Mock<IWinGetClient>(MockBehavior.Strict);
        client
            .Setup(candidate => candidate.Search("query", 50, null, It.IsAny<CancellationToken>()))
            .Returns(() => Delayed<IReadOnlyList<PackageInfo>>([]));

        var service = CreatePackageService(client);

        RunWithoutPumping(() => service.Search("query"));

        client.VerifyAll();
    }

    [Test]
    public void RetryingInstall_CompletesWithoutPumpingCallerSynchronizationContext()
    {
        var client = new Mock<IWinGetClient>(MockBehavior.Strict);
        var constrained = new InstallRequest
        {
            Architecture = PackageArchitecture.X64,
            Scope = PackageScope.System
        };

        client
            .Setup(candidate => candidate.Install(
                "id",
                It.Is<InstallRequest>(request => request.Architecture == PackageArchitecture.X64),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(() => Delayed(PackageOperationResult.Failure(
                PackageOperationStatus.NoApplicableInstallers,
                "NoApplicableInstallers")));
        client
            .Setup(candidate => candidate.Install(
                "id",
                It.Is<InstallRequest>(request =>
                    request.Architecture == PackageArchitecture.Default &&
                    request.Scope == PackageScope.Any),
                null,
                It.IsAny<CancellationToken>()))
            .Returns(() => Delayed(PackageOperationResult.Success()));

        var service = CreatePackageService(client);

        RunWithoutPumping(() => service.Install("id", constrained));

        client.VerifyAll();
    }

    [Test]
    public void AddSource_CompletesWithoutPumpingCallerSynchronizationContext()
    {
        var client = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        var progress = Mock.Of<IProgress<double>>();
        var request = new AddPackageSourceRequest("contoso", "https://contoso.example");
        client
            .Setup(candidate => candidate.AddSource(request, progress, It.IsAny<CancellationToken>()))
            .Returns(() => Delayed(SourceOperationResult.Success()));

        var service = CreateSourceService(client);

        RunWithoutPumping(() => service.AddSource(request, progress));

        client.VerifyAll();
    }

    [Test]
    public void RemoveSource_CompletesWithoutPumpingCallerSynchronizationContext()
    {
        var client = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        var progress = Mock.Of<IProgress<double>>();
        client
            .Setup(candidate => candidate.RemoveSource("contoso", true, progress, It.IsAny<CancellationToken>()))
            .Returns(() => Delayed(SourceOperationResult.Success()));

        var service = CreateSourceService(client);

        RunWithoutPumping(() => service.RemoveSource("contoso", preserveData: true, progress));

        client.VerifyAll();
    }

    [Test]
    public void RefreshSource_CompletesWithoutPumpingCallerSynchronizationContext()
    {
        var client = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        var progress = Mock.Of<IProgress<double>>();
        client
            .Setup(candidate => candidate.RefreshSource("winget", progress, It.IsAny<CancellationToken>()))
            .Returns(() => Delayed(SourceOperationResult.Success()));

        var service = CreateSourceService(client);

        RunWithoutPumping(() => service.RefreshSource("winget", progress));

        client.VerifyAll();
    }

    [Test]
    public void UpdateSource_CompletesWithoutPumpingCallerSynchronizationContext()
    {
        var client = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        client
            .Setup(candidate => candidate.UpdateSource("winget", true, 5, It.IsAny<CancellationToken>()))
            .Returns(() => Delayed(SourceOperationResult.Success()));

        var service = CreateSourceService(client);

        RunWithoutPumping(() => service.UpdateSource("winget", isExplicit: true, priority: 5));

        client.VerifyAll();
    }

    [Test]
    public async Task Install_CancellationBeforeRetry_DoesNotStartRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<IWinGetClient>(MockBehavior.Strict);
        client.Setup(candidate => candidate.Install("id", It.IsAny<InstallRequest>(), null, cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromResult(PackageOperationResult.Failure(PackageOperationStatus.NoApplicableInstallers, "retry"));
            });

        var act = async () => await CreatePackageService(client).Install("id", new InstallRequest { Architecture = PackageArchitecture.X64 }, cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        client.VerifyAll();
    }

    [Test]
    public async Task Update_CancellationBeforeRetry_DoesNotStartRetry()
    {
        using var cancellation = new CancellationTokenSource();
        var client = new Mock<IWinGetClient>(MockBehavior.Strict);
        client.Setup(candidate => candidate.Upgrade("id", It.IsAny<InstallRequest>(), null, cancellation.Token))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromResult(PackageOperationResult.Failure(PackageOperationStatus.NoApplicableUpgrade, "retry"));
            });

        var act = async () => await CreatePackageService(client).Update("id", new InstallRequest { Architecture = PackageArchitecture.X64 }, cancellationToken: cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        client.VerifyAll();
    }

    private static PackageManagementService CreatePackageService(Mock<IWinGetClient> client)
    {
        return new PackageManagementService(
            client.Object,
            Mock.Of<IWinGetCliClient>(),
            NullLogger<PackageManagementService>.Instance);
    }

    private static PackageSourceService CreateSourceService(Mock<IWinGetSourceClient> client)
    {
        return new PackageSourceService(
            client.Object,
            NullLogger<PackageSourceService>.Instance);
    }

    private static Task<T> Delayed<T>(T result)
    {
        return Task.Delay(50).ContinueWith(
            _ => result,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void RunWithoutPumping(Func<Task> operation)
    {
        Exception? failure = null;
        var context = new NonPumpingSynchronizationContext();
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);

            try
            {
                operation().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "SubZeroDev.WinGet non-pumping context test"
        };

        thread.Start();

        var completedWithoutPumping = false;

        try
        {
            completedWithoutPumping = thread.Join(CompletionTimeout);
        }
        finally
        {
            context.Release();
            thread.Join(CompletionTimeout).Should().BeTrue(
                "releasing queued continuations must allow the dedicated test thread to exit");
        }

        completedWithoutPumping.Should().BeTrue(
            "library continuations must not require the caller's non-pumping SynchronizationContext; {0} continuation(s) were posted",
            context.PostCount);

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _continuations = new();

        private int _postCount;

        private int _released;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref _postCount);

            if (Volatile.Read(ref _released) != 0)
            {
                ThreadPool.QueueUserWorkItem(_ => callback(state));

                return;
            }

            _continuations.Enqueue((callback, state));

            if (Volatile.Read(ref _released) != 0)
            {
                Drain();
            }
        }

        public void Release()
        {
            Interlocked.Exchange(ref _released, 1);
            Drain();
        }

        private void Drain()
        {
            while (_continuations.TryDequeue(out var continuation))
            {
                ThreadPool.QueueUserWorkItem(_ => continuation.Callback(continuation.State));
            }
        }
    }
}
