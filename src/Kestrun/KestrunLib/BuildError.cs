
using System.Management.Automation;
using System.Text;
using Serilog;

namespace KestrunLib
{
    /// <summary>
    /// Helper methods for formatting and returning PowerShell build errors.
    /// </summary>
    static class BuildError
    {
        /// <summary>
        /// Creates a text response containing the formatted error output from a
        /// <see cref="PowerShell"/> instance.
        /// </summary>
        /// <param name="ps">PowerShell instance whose streams contain the errors.</param>
        /// <returns>An <see cref="IResult"/> containing the error text with HTTP status 500.</returns>
        static public IResult Result(PowerShell ps)
        {
            // 500 + text body
            return Results.Text(content: Text(ps), statusCode: 500, contentType: "text/plain; charset=utf-8");
        }



        /// <summary>
        /// Formats all output streams from the provided <see cref="PowerShell"/>
        /// into a single human readable string.
        /// </summary>
        /// <param name="ps">PowerShell instance whose stream contents will be read.</param>
        /// <returns>Formatted error text containing errors, warnings and verbose messages.</returns>
        static public string Text(PowerShell ps)
        {
            ArgumentNullException.ThrowIfNull(ps);

            var errors = ps.Streams.Error.Select(e => e.ToString());
            var verbose = ps.Streams.Verbose.Select(v => v.ToString());
            var warnings = ps.Streams.Warning.Select(w => w.ToString());
            var debug = ps.Streams.Debug.Select(d => d.ToString());
            var info = ps.Streams.Information.Select(i => i.ToString());
            // Format the output
            // 500 + text body

            var sb = new StringBuilder();

            void append(string emoji, IEnumerable<string> lines)
            {
                if (!lines.Any()) return;
                sb.AppendLine($"{emoji}[{emoji switch
                {
                    "❌" => "Error",
                    "💬" => "Verbose",
                    "⚠️" => "Warning",
                    "🐞" => "Debug",
                    _ => "Info"
                }}]");
                foreach (var l in lines) sb.AppendLine($"\t{l}");
            }

            append("❌", errors);
            append("💬", verbose);
            append("⚠️", warnings);
            append("🐞", debug);
            append("ℹ️", info);

            var msg = sb.ToString();
            Log.Information(msg);


            return msg;
        }

        /// <summary>
        /// Writes the formatted PowerShell error output directly to an
        /// <see cref="HttpContext"/> response with status code 500.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <param name="ps">The PowerShell instance containing error information.</param>
        /// <returns>A task that completes when the response body has been written.</returns>
        static public Task ResponseAsync(HttpContext context, PowerShell ps)
        {
            var errText = BuildError.Text(ps);               // plain string
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain; charset=utf-8";
            return context.Response.WriteAsync(errText);    // returns Task
        }

    }
}