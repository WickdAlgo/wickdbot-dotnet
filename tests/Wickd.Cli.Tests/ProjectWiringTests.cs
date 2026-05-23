using Wickd;

namespace Wickd.Tests;

/// <summary>
/// Verifies that the test project is wired to the application assembly.
/// </summary>
public class ProjectWiringTests
{
    [Fact]
    public void TestsCanReferenceInternalApplicationTypes()
    {
        // Referencing Program proves the test assembly can compile against internal app types.
        Assert.Equal("Wickd.Program", typeof(Program).FullName);
    }
}
