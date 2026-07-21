using FluentAssertions;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetUnavailableExceptionTests
{
    [Test]
    public void CarriesMessage_WithoutInnerException()
    {
        var ex = new WinGetUnavailableException("WinGet is not installed.");

        ex.Message.Should().Be("WinGet is not installed.");
        ex.InnerException.Should().BeNull();
    }

    [Test]
    public void CarriesInnerException_ForDiagnostics()
    {
        var inner = new InvalidOperationException("COM activation failed");

        var ex = new WinGetUnavailableException("WinGet COM activation failed.", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }
}
