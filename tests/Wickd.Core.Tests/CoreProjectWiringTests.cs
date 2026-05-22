using Wickd.Models;

namespace Wickd.Tests;

/// <summary>
/// Verifies that the core test project is wired to the core assembly.
/// </summary>
public class CoreProjectWiringTests
{
    [Fact]
    public void TestsCanReferenceCoreTypes()
    {
        Assert.Equal("Wickd.Models.Timeframe", typeof(Timeframe).FullName);
    }
}
