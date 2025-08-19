
using System.Reflection;
using Kestrun.Scripting;
using Kestrun.Utilities;

namespace Kestrun.Hosting.Options;
/// <summary>
/// Options for mapping a route, including pattern, HTTP verbs, script code, authorization, and metadata.
/// </summary>
public record MapRouteOptions
{
    /// <summary>
    /// The route pattern to match for this option.
    /// </summary>
    public string? Pattern { get; init; }
    /// <summary>
    /// The HTTP verbs (methods) that this route responds to.
    /// </summary>
    public IEnumerable<HttpVerb> HttpVerbs { get; init; } = [];
    /// <summary>
    /// The script code to execute for this route.
    /// </summary>
    public string? Code { get; init; }
    /// <summary>
    /// The scripting language used for the route's code.
    /// </summary>
    public ScriptLanguage Language { get; init; } = ScriptLanguage.PowerShell;
    /// <summary>
    /// Additional import namespaces required for the script code.
    /// </summary>
    public string[]? ExtraImports { get; init; }
    /// <summary>
    /// Additional assembly references required for the script code.
    /// </summary>
    public Assembly[]? ExtraRefs { get; init; }
    /// <summary>
    /// Authorization Scheme names required for this route.
    /// </summary>
    public string[] RequireSchemes { get; init; } = []; // Authorization scheme name, if any
    /// <summary>
    /// Authorization policy names required for this route.
    /// </summary>
    public string[]? RequirePolicies { get; init; } = []; // Authorization policies, if any
    /// <summary>
    /// Name of the CORS policy to apply, if any.
    /// </summary>
    public string CorsPolicyName { get; init; } = string.Empty; // Name of the CORS policy to apply, if any
    /// <summary>
    /// If true, short-circuits the pipeline after this route.
    /// </summary>
    public bool ShortCircuit { get; internal set; } // If true, short-circuit the pipeline after this route
    /// <summary>
    /// Status code to return if short-circuiting the pipeline after this route.
    /// </summary>
    public int? ShortCircuitStatusCode { get; internal set; } = null; // Status code to return if short-circuiting
    /// <summary>
    /// If true, allows anonymous access to this route.
    /// </summary>
    public bool AllowAnonymous { get; internal set; }
    /// <summary>
    /// If true, disables antiforgery protection for this route.
    /// </summary>
    public bool DisableAntiforgery { get; internal set; }
    /// <summary>
    /// The name of the rate limit policy to apply to this route, if any.
    /// </summary>
    public string? RateLimitPolicyName { get; internal set; }

    /// <summary>
    /// Additional metadata for the route, represented as key-value pairs.
    /// </summary>
    public Dictionary<string, object?>? Arguments { get; init; } = []; // Additional metadata for the route

    /// <summary>
    /// Metadata for OpenAPI documentation related to the route.
    /// </summary>
    public record OpenAPIMetadata
    {
        /// <summary>
        /// A brief summary of the route for OpenAPI documentation.
        /// </summary>
        public string? Summary { get; init; }
        /// <summary>
        /// A detailed description of the route for OpenAPI documentation.
        /// </summary>
        public string? Description { get; init; }
        /// <summary>
        /// The unique operation ID for the route in OpenAPI documentation.
        /// </summary>
        public string? OperationId { get; init; }
        /// <summary>
        /// Comma-separated tags for OpenAPI documentation.
        /// </summary>
        public string[] Tags { get; init; } = []; // Comma-separated tags
        /// <summary>
        /// Group name for OpenAPI documentation.
        /// </summary>
        public string? GroupName { get; init; } // Group name for OpenAPI documentation 
    }

    /// <summary>
    /// OpenAPI metadata for this route.
    /// </summary>
    public OpenAPIMetadata OpenAPI { get; init; } = new OpenAPIMetadata(); // OpenAPI metadata for this route

    /// <summary>
    /// If true, throws an exception on duplicate routes.
    /// </summary>
    public bool ThrowOnDuplicate { get; init; }
}
