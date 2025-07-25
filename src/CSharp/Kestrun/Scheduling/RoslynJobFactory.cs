using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace Kestrun.Scheduling;
internal static class RoslynJobFactory
{
    private static readonly MetadataReference[] _refs =
    {
        MetadataReference.CreateFromFile(typeof(object        ).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Task          ).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(CancellationToken).Assembly.Location),
        // add Kestrun assemblies so the script can use Host / SharedState
        MetadataReference.CreateFromFile(typeof(KestrunHost   ).Assembly.Location)
    };

    /// <summary>
    /// Compiles C# source into a <c>Func&lt;CancellationToken,Task&gt;</c>.
    /// </summary>
    public static Func<CancellationToken,Task> Build(string source)
    {
        const string tpl = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Kestrun;

        public static class __KrJob
        {
            public static async Task Run(CancellationToken ct)
            {
                #line 1
                {{USER_CODE}}
            }
        }
        """;

        var code = tpl.Replace("{{USER_CODE}}", source);

        var comp = CSharpCompilation.Create(
            assemblyName: $"KrJob_{Guid.NewGuid():N}",
            new[] { CSharpSyntaxTree.ParseText(code) },
            _refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                         optimizationLevel: OptimizationLevel.Release));

        using var ms = new MemoryStream();
        EmitResult emit = comp.Emit(ms);
        if (!emit.Success)
        {
            var errs = string.Join(Environment.NewLine, emit.Diagnostics);
            throw new InvalidOperationException($"Roslyn compile failed:{Environment.NewLine}{errs}");
        }

        ms.Position = 0;
        var asm = Assembly.Load(ms.ToArray());
        var run = asm.GetType("__KrJob")!.GetMethod("Run", BindingFlags.Static | BindingFlags.Public)!;

        return ct => (Task)run.Invoke(null, new object?[] { ct })!;
    }
}
