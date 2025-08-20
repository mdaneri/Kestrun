using Kestrun.Scripting;
using Xunit;


namespace KestrunTests.Scripting;
public class KestrunRunspacePoolManagerTest
{
    [Fact]
    public void MaxRunspaces_ReturnsConfiguredMax()
    {
        // Arrange
        int minRunspaces = 1;
        int maxRunspaces = 5;
        var manager = new KestrunRunspacePoolManager(minRunspaces, maxRunspaces);

        // Act
        int actualMax = manager.MaxRunspaces;

        // Assert
        Assert.Equal(maxRunspaces, actualMax);
    }
}
