using System.Runtime.InteropServices;

using FluentAssertions;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetClientVersionTests
{
    [Test]
    public void ReadWinGetVersion_ReturnsSuppliedVersionUnchanged()
    {
        WinGetClient.ReadWinGetVersion(() => "v1.29.280", CancellationToken.None)
            .Should().Be("v1.29.280");
    }

    [Test]
    public void ReadWinGetVersion_WhenVersionMemberIsUnavailable_ReturnsNull()
    {
        WinGetClient.ReadWinGetVersion(
                () => throw new VersionMemberInvalidCastException(unchecked((int)0x80004002)),
                CancellationToken.None)
            .Should().BeNull();
    }

    [TestCase(unchecked((int)0x80004001))]
    [TestCase(unchecked((int)0x80004005))]
    public void ReadWinGetVersion_WhenInvalidCastHasAnotherHResult_PropagatesOriginalFailure(int hresult)
    {
        var expected = new VersionMemberInvalidCastException(hresult);

        var act = () => WinGetClient.ReadWinGetVersion(() => throw expected, CancellationToken.None);

        act.Should().Throw<VersionMemberInvalidCastException>().Which.Should().BeSameAs(expected);
    }

    [Test]
    public void ReadWinGetVersion_WhenComAccessFails_PropagatesOriginalFailure()
    {
        var expected = new COMException("COM failed", unchecked((int)0x80004005));

        var act = () => WinGetClient.ReadWinGetVersion(() => throw expected, CancellationToken.None);

        act.Should().Throw<COMException>().Which.Should().BeSameAs(expected);
    }

    [Test]
    public void ReadWinGetVersion_WhenActivationOrProjectionFails_PropagatesOriginalFailure()
    {
        var expected = new InvalidOperationException("activation failed");

        var act = () => WinGetClient.ReadWinGetVersion(() => throw expected, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expected);
    }

    [Test]
    public void ReadWinGetVersion_WhenCallerTokenIsCancelled_ThrowsAndDoesNotReadVersion()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var read = false;

        var act = () => WinGetClient.ReadWinGetVersion(() =>
        {
            read = true;
            return "v1.29.280";
        }, cancellation.Token);

        act.Should().Throw<OperationCanceledException>();
        read.Should().BeFalse();
    }

    private sealed class VersionMemberInvalidCastException : InvalidCastException
    {
        internal VersionMemberInvalidCastException(int hresult)
            : base("Version member cast failed")
        {
            HResult = hresult;
        }
    }
}
