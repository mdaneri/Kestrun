using Kestrun;

namespace Kestrun
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate,
                 AllowMultiple = false)]
    public sealed class ScriptLanguageAttribute(ScriptLanguage lang) : Attribute
    {
        public ScriptLanguage Language { get; } = lang;
    }
    public static class RouteHandlerBuilderLanguageExtensions
    {
        // nice fluent helper
        public static RouteHandlerBuilder WithLanguage(
                 this RouteHandlerBuilder b, ScriptLanguage lang) =>
             b.WithMetadata(new ScriptLanguageAttribute(lang));
    }

    public static class LanguageRuntimeExtensions
    {
        /// Adds <see cref="configure"/> to a sub-pipeline that runs
        /// only when the resolved endpoint is tagged with the given language.
        public static IApplicationBuilder UseLanguageRuntime(
            this IApplicationBuilder app,
            ScriptLanguage language,
            Action<IApplicationBuilder> configure)
        {
            return app.UseWhen(ctx =>
            {
                ScriptLanguageAttribute? attr =
                    ctx.GetEndpoint()?.Metadata
                       .GetMetadata<ScriptLanguageAttribute>();
                return attr?.Language == language;
            }, configure);
        }
    }

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
}