using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Kestrun;

/// <summary>
/// Exception thrown when C# script compilation fails.
/// Contains detailed diagnostic information about compilation errors.
/// </summary>
public class CompilationErrorException : Exception
{
    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public CompilationErrorException(string message, ImmutableArray<Diagnostic> diagnostics)
        : base(FormatMessage(message, diagnostics))
    {
        Diagnostics = diagnostics;
    }

    public CompilationErrorException(string message, ImmutableArray<Diagnostic> diagnostics, Exception innerException)
        : base(FormatMessage(message, diagnostics), innerException)
    {
        Diagnostics = diagnostics;
    }

    private static string FormatMessage(string baseMessage, ImmutableArray<Diagnostic> diagnostics)
    {
        var sb = new StringBuilder();
        sb.AppendLine(baseMessage);
        sb.AppendLine();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();

        if (errors.Any())
        {
            sb.AppendLine($"Compilation Errors ({errors.Length}):");
            for (int i = 0; i < errors.Length; i++)
            {
                var error = errors[i];
                sb.AppendLine($"  {i + 1}. {FormatDiagnostic(error)}");
            }
            sb.AppendLine();
        }

        if (warnings.Any())
        {
            sb.AppendLine($"Compilation Warnings ({warnings.Length}):");
            for (int i = 0; i < warnings.Length; i++)
            {
                var warning = warnings[i];
                sb.AppendLine($"  {i + 1}. {FormatDiagnostic(warning)}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        var position = location.IsInSource ? $" at line {location.GetLineSpan().StartLinePosition.Line + 1}, column {location.GetLineSpan().StartLinePosition.Character + 1}" : "";

        return $"[{diagnostic.Id}] {diagnostic.GetMessage()}{position}";
    }

    /// <summary>
    /// Gets a formatted string containing all error details.
    /// </summary>
    public string GetDetailedErrorMessage()
    {
        return Message;
    }

    /// <summary>
    /// Gets only the error diagnostics (excluding warnings).
    /// </summary>
    public IEnumerable<Diagnostic> GetErrors()
    {
        return Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Gets only the warning diagnostics.
    /// </summary>
    public IEnumerable<Diagnostic> GetWarnings()
    {
        return Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);
    }
}
