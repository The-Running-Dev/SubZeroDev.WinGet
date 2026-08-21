using FluentAssertions;

using SubZeroDev.WinGet.Com;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetActivationModeSelectorTests
{
    [Test]
    public void Create_WhenFallbacksAreNeeded_AttemptsModesInOrderAndReturnsFirstSuccess()
    {
        var selector = new WinGetActivationModeSelector();
        var attempts = new List<WinGetActivationMode>();

        var result = selector.Create(mode =>
        {
            attempts.Add(mode);

            return mode switch
            {
                WinGetActivationMode.Projection => throw new InvalidOperationException("projection failed"),
                WinGetActivationMode.LocalServer => throw new InvalidOperationException("local server failed"),
                WinGetActivationMode.LocalServerLowerTrust => "activated",
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        });

        result.Should().Be("activated");
        attempts.Should().Equal(
            WinGetActivationMode.Projection,
            WinGetActivationMode.LocalServer,
            WinGetActivationMode.LocalServerLowerTrust);
    }

    [Test]
    public void Create_AfterSuccess_UsesOnlyTheCachedMode()
    {
        var selector = new WinGetActivationModeSelector();
        var attempts = new List<WinGetActivationMode>();

        selector.Create(mode =>
        {
            attempts.Add(mode);
            return "first";
        });

        var result = selector.Create(mode =>
        {
            attempts.Add(mode);
            return "second";
        });

        result.Should().Be("second");
        attempts.Should().Equal(WinGetActivationMode.Projection, WinGetActivationMode.Projection);
    }

    [Test]
    public void Create_WhenTheCachedModeFails_PropagatesWithoutReselecting()
    {
        var selector = new WinGetActivationModeSelector();
        selector.Create(_ => "first");
        var expected = new InvalidOperationException("cached activation failed");
        var attempts = new List<WinGetActivationMode>();

        var act = () => selector.Create<string>(mode =>
        {
            attempts.Add(mode);
            throw expected;
        });

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expected);
        attempts.Should().Equal(WinGetActivationMode.Projection);
    }

    [Test]
    public void Create_WhenEveryInitialModeFails_AggregatesFailuresAndAllowsALaterRetry()
    {
        var selector = new WinGetActivationModeSelector();
        var projectionFailure = new InvalidOperationException("projection failed");
        var localServerFailure = new InvalidOperationException("local server failed");
        var lowerTrustFailure = new InvalidOperationException("lower trust failed");
        var attempts = new List<WinGetActivationMode>();

        var act = () => selector.Create<string>(mode =>
        {
            attempts.Add(mode);

            throw mode switch
            {
                WinGetActivationMode.Projection => projectionFailure,
                WinGetActivationMode.LocalServer => localServerFailure,
                WinGetActivationMode.LocalServerLowerTrust => lowerTrustFailure,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        });

        var unavailable = act.Should().Throw<WinGetUnavailableException>().Which;
        unavailable.InnerException.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().Equal(projectionFailure, localServerFailure, lowerTrustFailure);

        var retry = selector.Create(mode =>
        {
            attempts.Add(mode);
            return "retry succeeded";
        });

        retry.Should().Be("retry succeeded");
        attempts.Should().Equal(
            WinGetActivationMode.Projection,
            WinGetActivationMode.LocalServer,
            WinGetActivationMode.LocalServerLowerTrust,
            WinGetActivationMode.Projection);
    }

    [Test]
    public async Task Create_ConcurrentFirstCallers_SerializeSelectionAndShareTheWinningMode()
    {
        var selector = new WinGetActivationModeSelector();
        var projectionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProjection = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new List<WinGetActivationMode>();
        var attemptsGate = new object();

        string Attempt(WinGetActivationMode mode)
        {
            lock (attemptsGate)
            {
                attempts.Add(mode);
            }

            if (mode == WinGetActivationMode.Projection)
            {
                projectionStarted.TrySetResult();
                releaseProjection.Task.GetAwaiter().GetResult();
                throw new InvalidOperationException("projection failed");
            }

            return "activated";
        }

        var first = Task.Run(() => selector.Create(Attempt));
        await projectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var second = Task.Run(() => selector.Create(Attempt));
        releaseProjection.TrySetResult();

        (await Task.WhenAll(first, second)).Should().OnlyContain(result => result == "activated");
        attempts.Should().Equal(
            WinGetActivationMode.Projection,
            WinGetActivationMode.LocalServer,
            WinGetActivationMode.LocalServer);
    }
}
