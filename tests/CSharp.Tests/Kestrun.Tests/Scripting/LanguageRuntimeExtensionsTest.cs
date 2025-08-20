using Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Kestrun.Scripting;

namespace KestrunTests.Scripting;

public class LanguageRuntimeExtensionsTest
{
    [Fact]
    public void ScriptLanguageAttribute_SetsLanguageProperty()
    {
        var attr = new ScriptLanguageAttribute(ScriptLanguage.CSharp);
        Assert.Equal(ScriptLanguage.CSharp, attr.Language);
    }
    /*
        [Fact]
        public void RouteHandlerBuilder_WithLanguage_AddsMetadata()
        {
            var builder = new TestEndpointConventionBuilder();
            // Ensure WithLanguage extension method is available
            builder.WithLanguage(ScriptLanguage.FSharp);

            Assert.Single(builder.Metadata);
            var attr = builder.Metadata[0] as ScriptLanguageAttribute;
            Assert.NotNull(attr);
            Assert.Equal(ScriptLanguage.FSharp, attr!.Language);
        }

        [Fact]
        public void EndpointConventionBuilder_WithLanguage_AddsMetadata()
        {
            var builder = new TestEndpointConventionBuilder();
            builder.WithLanguage(ScriptLanguage.Python);

            Assert.Single(builder.Metadata);
            var attr = builder.Metadata[0] as ScriptLanguageAttribute;
            Assert.NotNull(attr);
            Assert.Equal(ScriptLanguage.Python, attr!.Language);
        }

        [Fact]
        public void UseLanguageRuntime_InvokesConfigureOnlyForMatchingLanguage()
        {
            bool configureCalled = false;
            var appBuilder = new ApplicationBuilder(new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider());

            appBuilder.UseLanguageRuntime(ScriptLanguage.CSharp, ab => configureCalled = true);

            var endpoint = new Endpoint(
                context => Task.CompletedTask,
                new EndpointMetadataCollection(new ScriptLanguageAttribute(ScriptLanguage.CSharp)),
                "test"
            );
            var context = new DefaultHttpContext();
            context.SetEndpoint(endpoint);

            // Simulate middleware pipeline
            var middleware = appBuilder.Build();
            middleware(context);

            Assert.True(configureCalled);
        }
    */
    // Helper classes for testing
    private class TestEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<object> Metadata { get; } = [];
        public void Add(Action<EndpointBuilder> convention)
        {
            var builder = new TestEndpointBuilder();
            convention(builder);
            Metadata.AddRange(builder.Metadata);
        }
    }

    private class TestEndpointBuilder : EndpointBuilder
    {
        public new IList<object> Metadata { get; } = [];
        public new RequestDelegate? RequestDelegate { get; set; }
        public new string? DisplayName { get; set; }

        public override Endpoint Build()
        {
            return new Endpoint(
                RequestDelegate ?? (context => Task.CompletedTask),
                new EndpointMetadataCollection(Metadata),
                DisplayName
            );
        }
    }
}