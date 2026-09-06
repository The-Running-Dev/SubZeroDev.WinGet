using FluentAssertions;

using Microsoft.Management.Deployment;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetClientReachabilityTests
{
    [Test]
    public void ProbeReachable_WhenConnectSucceeds_ReturnsTrue()
    {
        WinGetClient.ProbeReachable(() => ConnectResultStatus.Ok).Should().BeTrue();
    }

    [Test]
    public void ProbeReachable_WhenConnectReturnsNonOkStatus_ReturnsFalse()
    {
        WinGetClient.ProbeReachable(() => ConnectResultStatus.CatalogError).Should().BeFalse();
    }

    [Test]
    public void ProbeReachable_WhenConnectThrows_ReturnsFalseRatherThanPropagating()
    {
        WinGetClient.ProbeReachable(() => throw new InvalidOperationException("unreachable"))
            .Should().BeFalse();
    }

    [Test]
    public void ProbeReachable_WhenCancelled_PropagatesRatherThanReportingUnreachable()
    {
        var act = () => WinGetClient.ProbeReachable(() => throw new OperationCanceledException());

        act.Should().Throw<OperationCanceledException>();
    }
}
