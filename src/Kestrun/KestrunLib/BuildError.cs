
using System.Management.Automation;
using System.Text;

namespace KestrunLib
{
    static class BuildError
    {

        static public IResult Result(PowerShell ps)
        {
            // 500 + text body
            return Results.Text(content: Text(ps), statusCode: 500, contentType: "text/plain; charset=utf-8");
        }



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
            Console.WriteLine(msg);


            return msg;
        }
    }
}