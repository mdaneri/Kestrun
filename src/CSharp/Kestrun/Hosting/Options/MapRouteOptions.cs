
using System.Reflection;
using Kestrun.Scripting;
using Kestrun.Utilities;

namespace Kestrun.Hosting.Options;
public record MapRouteOptions
{
    public string? Pattern { get; init; }
    public IEnumerable<HttpVerb> HttpVerbs { get; init; } = [];
    public string? Code { get; init; }
    public ScriptLanguage Language { get; init; } = ScriptLanguage.PowerShell;
    public string[]? ExtraImports { get; init; }
    public Assembly[]? ExtraRefs { get; init; }
    public string[] RequireAuthorization { get; init; } = []; // Authorization policy name, if any
    public string CorsPolicyName { get; init; } = string.Empty; // Name of the CORS policy to apply, if any
    public bool ShortCircuit { get; internal set; } = false; // If true, short-circuit the pipeline after this route
    public int? ShortCircuitStatusCode { get; internal set; } = null; // Status code to return if short-circuiting
    public bool AllowAnonymous { get; internal set; }
    public bool DisableAntiforgery { get; internal set; }
    public string? RateLimitPolicyName { get; internal set; }

    public Dictionary<string,object> Arguments { get; init; } = []; // Additional metadata for the route

    public record OpenAPIMetadata
    {
        public string? Summary { get; init; }
        public string? Description { get; init; }
        public string? OperationId { get; init; }
        public string[] Tags { get; init; } = []; // Comma-separated tags
        public string? GroupName { get; init; } // Group name for OpenAPI documentation 
    };

    public OpenAPIMetadata OpenAPI { get; init; } = new OpenAPIMetadata(); // OpenAPI metadata for this route
}
