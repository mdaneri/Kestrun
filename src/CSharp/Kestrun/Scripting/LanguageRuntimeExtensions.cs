using Kestrun;

namespace Kestrun.Scripting;

/// <summary>
/// Attribute to specify the script language for a method or delegate.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ScriptLanguageAttribute"/> class.
/// </remarks>
/// <param name="lang">The script language to associate with the method or delegate.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate,
             AllowMultiple = false)]
public sealed class ScriptLanguageAttribute(ScriptLanguage lang) : Attribute
{
    /// <summary>
    /// Gets the script language associated with this attribute.
    /// </summary>
    public ScriptLanguage Language { get; } = lang;
}
/// <summary>
/// Extension methods for <see cref="RouteHandlerBuilder"/> to support script language metadata.
/// </summary>
public static class RouteHandlerBuilderLanguageExtensions
{
    /// <summary>
    /// Tags a <see cref="RouteHandlerBuilder"/> with a <see cref="ScriptLanguageAttribute"/> for the specified language.
    /// </summary>
    /// <param name="b">The route handler builder to tag.</param>
    /// <param name="lang">The script language to associate with the route handler.</param>
    /// <returns>The same <see cref="RouteHandlerBuilder"/> instance for fluent chaining.</returns>
    public static RouteHandlerBuilder WithLanguage(
             this RouteHandlerBuilder b, ScriptLanguage lang) =>
         b.WithMetadata(new ScriptLanguageAttribute(lang));
}

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to support language-based runtime configuration.
/// </summary>
public static class LanguageRuntimeExtensions
{
    /// <summary>
    /// Configures the application pipeline to use a specific language runtime for endpoints tagged with the given <see cref="ScriptLanguage"/>.
    /// </summary>
    /// <param name="app">The application builder to configure.</param>
    /// <param name="language">The script language to filter endpoints by.</param>
    /// <param name="configure">The configuration action to apply when the language matches.</param>
    /// <returns>The configured <see cref="IApplicationBuilder"/> instance.</returns>
    public static IApplicationBuilder UseLanguageRuntime(
        this IApplicationBuilder app,
        ScriptLanguage language,
        Action<IApplicationBuilder> configure)
    {
        return app.UseWhen(context =>
        {
            ScriptLanguageAttribute? attr =
                context.GetEndpoint()?.Metadata
                   .GetMetadata<ScriptLanguageAttribute>();
            return attr?.Language == language;
        }, configure);
    }
}

/// <summary>
/// Extension methods for <see cref="IEndpointConventionBuilder"/> to support script language metadata.
/// </summary>
public static class EndpointConventionBuilderLanguageExtensions
{
    /// <summary>
    /// Tags any endpoint builder with <see cref="ScriptLanguageAttribute"/>.
    /// Works for RouteHandlerBuilder, RouteGroupBuilder, etc.
    /// </summary>
    public static TBuilder WithLanguage<TBuilder>(
        this TBuilder builder, ScriptLanguage language)
        where TBuilder : IEndpointConventionBuilder
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        // IEndpointConventionBuilder exposes Add(Action<EndpointBuilder>)
        builder.Add(ep => ep.Metadata.Add(new ScriptLanguageAttribute(language)));
        return builder;   // keep fluent chaining
    }
}
