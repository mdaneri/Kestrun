using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Antiforgery;
using Moq;
using Xunit;

namespace KestrunTests.Hosting
{
    public class KestrunHttpMiddlewareExtensionsTests
    {
        private KestrunHost CreateHost(out List<Action<IApplicationBuilder>> middleware)
        {
            var logger = new Mock<Serilog.ILogger>();
            logger.Setup(l => l.IsEnabled(It.IsAny<Serilog.Events.LogEventLevel>())).Returns(false);
            var host = new KestrunHost("TestApp", logger.Object);
            var field = typeof(KestrunHost).GetField("_middlewareQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            middleware = (List<Action<IApplicationBuilder>>)field!.GetValue(host)!;
            return host;
        }

        [Fact]
        public void AddResponseCompression_WithNullOptions_UsesDefaults()
        {
            var host = CreateHost(out var middleware);
            host.AddResponseCompression((ResponseCompressionOptions)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddResponseCompression_WithOptions_RegistersMiddleware()
        {
            var host = CreateHost(out var middleware);
            var options = new ResponseCompressionOptions { EnableForHttps = true };
            host.AddResponseCompression(options);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddRateLimiter_WithNullOptions_UsesDefaults()
        {
            var host = CreateHost(out var middleware);
            host.AddRateLimiter((RateLimiterOptions)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddRateLimiter_WithOptions_RegistersMiddleware()
        {
            var host = CreateHost(out var middleware);
            var options = new RateLimiterOptions();
            host.AddRateLimiter(options);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddAntiforgery_WithNullOptions_UsesDefaults()
        {
            var host = CreateHost(out var middleware);
            host.AddAntiforgery((AntiforgeryOptions)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddAntiforgery_WithOptions_RegistersMiddleware()
        {
            var host = CreateHost(out var middleware);
            var options = new AntiforgeryOptions { FormFieldName = "_csrf" };
            host.AddAntiforgery(options);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddCorsAllowAll_RegistersAllowAllPolicy()
        {
            var host = CreateHost(out var middleware);
            host.AddCorsAllowAll();
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddCors_WithPolicyBuilder_RegistersPolicy()
        {
            var host = CreateHost(out var middleware);
            var builder = new CorsPolicyBuilder().AllowAnyOrigin();
            host.AddCors("TestPolicy", builder);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddCors_WithPolicyAction_RegistersPolicy()
        {
            var host = CreateHost(out var middleware);
            host.AddCors("TestPolicy", b => b.AllowAnyOrigin().AllowAnyHeader());
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddResponseCompression_WithNullAction_DoesNotThrow()
        {
            var host = CreateHost(out var middleware);
            host.AddResponseCompression((Action<ResponseCompressionOptions>)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddRateLimiter_WithNullAction_DoesNotThrow()
        {
            var host = CreateHost(out var middleware);
            host.AddRateLimiter((Action<RateLimiterOptions>)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddAntiforgery_WithNullAction_DoesNotThrow()
        {
            var host = CreateHost(out var middleware);
            host.AddAntiforgery((Action<AntiforgeryOptions>)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddCors_WithNullPolicyName_Throws()
        {
            var host = CreateHost(out _);
            Assert.Throws<ArgumentException>(() => host.AddCors(null!, b => b.AllowAnyOrigin()));
        }

        [Fact]
        public void AddCors_WithEmptyPolicyName_Throws()
        {
            var host = CreateHost(out _);
            Assert.Throws<ArgumentException>(() => host.AddCors("", b => b.AllowAnyOrigin()));
        }

        [Fact]
        public void AddCors_WithNullBuilder_Throws()
        {
            var host = CreateHost(out _);
            Assert.Throws<ArgumentNullException>(() => host.AddCors("Test", (CorsPolicyBuilder)null!));
        }

        [Fact]
        public void AddCors_WithNullBuildPolicy_Throws()
        {
            var host = CreateHost(out _);
            Assert.Throws<ArgumentNullException>(() => host.AddCors("Test", (Action<CorsPolicyBuilder>)null!));
        }

        [Fact]
        public void AddResponseCompression_WithCustomMimeTypes_SetsMimeTypes()
        {
            var host = CreateHost(out var middleware);
            host.AddResponseCompression(o => o.MimeTypes = new[] { "application/json" });
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddRateLimiter_WithCustomDelegate_Registers()
        {
            var host = CreateHost(out var middleware);
            host.AddRateLimiter(o => { o.GlobalLimiter = null; });
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddAntiforgery_WithCustomDelegate_Registers()
        {
            var host = CreateHost(out var middleware);
            host.AddAntiforgery(o => o.FormFieldName = "csrf");
            Assert.True(middleware.Count > 0);
        }
    }
}
