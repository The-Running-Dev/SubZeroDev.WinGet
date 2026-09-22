using System.Runtime.InteropServices;

using FluentAssertions;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetProjectionMapperPriorityTests
{
    [Test]
    public void ReadPriority_ReturnsSuppliedPriorityUnchanged()
    {
        WinGetProjectionMapper.ReadPriority(() => 5).Should().Be(5);
    }

    [Test]
    public void ReadPriority_WhenPriorityMemberIsUnavailable_ReturnsZero()
    {
        WinGetProjectionMapper.ReadPriority(
                () => throw new PriorityMemberInvalidCastException(unchecked((int)0x80004002)))
            .Should().Be(0);
    }

    [TestCase(unchecked((int)0x80004001))]
    [TestCase(unchecked((int)0x80004005))]
    public void ReadPriority_WhenInvalidCastHasAnotherHResult_PropagatesOriginalFailure(int hresult)
    {
        var expected = new PriorityMemberInvalidCastException(hresult);

        var act = () => WinGetProjectionMapper.ReadPriority(() => throw expected);

        act.Should().Throw<PriorityMemberInvalidCastException>().Which.Should().BeSameAs(expected);
    }

    [Test]
    public void ReadPriority_WhenComAccessFails_PropagatesOriginalFailure()
    {
        var expected = new COMException("COM failed", unchecked((int)0x80004005));

        var act = () => WinGetProjectionMapper.ReadPriority(() => throw expected);

        act.Should().Throw<COMException>().Which.Should().BeSameAs(expected);
    }

    [Test]
    public void ReadPriority_WhenOperationIsCancelled_PropagatesOriginalFailure()
    {
        var expected = new OperationCanceledException();

        var act = () => WinGetProjectionMapper.ReadPriority(() => throw expected);

        act.Should().Throw<OperationCanceledException>().Which.Should().BeSameAs(expected);
    }

    private sealed class PriorityMemberInvalidCastException : InvalidCastException
    {
        internal PriorityMemberInvalidCastException(int hresult)
            : base("Priority member cast failed")
        {
            HResult = hresult;
        }
    }
}
