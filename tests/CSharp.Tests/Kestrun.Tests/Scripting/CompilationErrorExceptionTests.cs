using Kestrun;
using Kestrun.Scripting;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Xunit;

namespace KestrunTests.Scripting;
public class CompilationErrorExceptionTests

{
    private static Diagnostic MakeDiag(string msg, DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor("ID", "Title", msg, "Cat", severity, true);
        return Diagnostic.Create(descriptor, Location.None);
    }

    [Fact]
    public void GetErrorsAndWarnings_Work()
    {
        var diags = ImmutableArray.Create(
            MakeDiag("err", DiagnosticSeverity.Error),
            MakeDiag("warn", DiagnosticSeverity.Warning));
        var ex = new CompilationErrorException("bad", diags);
        Assert.Single(ex.GetErrors());
        Assert.Single(ex.GetWarnings());
        Assert.Contains("err", ex.GetDetailedErrorMessage());
    }
}
