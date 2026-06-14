using Xunit;

namespace StashValueTracker.Tests;

public class SmokeTest
{
    [Fact]
    public void Sanity() => Assert.Equal(4, 2 + 2);
}
