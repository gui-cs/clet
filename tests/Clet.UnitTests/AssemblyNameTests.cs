using Xunit;

namespace Clet.UnitTests;

public class AssemblyNameTests
{
    [Fact]
    public void CletAssembly_IsLowercase ()
    {
        string? name = typeof (Program).Assembly.GetName ().Name;

        Assert.Equal ("clet", name);
    }
}
