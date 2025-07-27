using Kestrun.Utilities;
using System.IO;
using System.Reflection;
using Xunit;

#pragma warning disable CA1050 // Declare types in namespaces
public class PowerShellModuleLocatorTests
#pragma warning restore CA1050 // Declare types in namespaces
{
    [Fact]
    public void FindFileUpwards_FindsFile()
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "a", "b"));
            var targetDir = Directory.CreateDirectory(Path.Combine(root.FullName, "a"));
            var file = Path.Combine(targetDir.FullName, "test.txt");
            File.WriteAllText(file, "data");

            var method = typeof(PowerShellModuleLocator).GetMethod("FindFileUpwards", BindingFlags.NonPublic | BindingFlags.Static)!;
            string? found = (string?)method.Invoke(null, new object[] { nested.FullName, Path.Combine("..", "test.txt") });
            Assert.Equal(Path.GetFullPath(file), Path.GetFullPath(found!));
        }
        finally
        {
            root.Delete(true);
        }
    }
}
