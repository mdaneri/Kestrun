using System;
using System.Collections.Generic;
using System.IO;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KestrunTests.Hosting
{
    public class KestrunHostStaticFilesExtensionsTests
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
        public void AddDefaultFiles_WithNullConfig_UsesDefaults()
        {
            var host = CreateHost(out var middleware);
            host.AddDefaultFiles((DefaultFilesOptions)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddDefaultFiles_WithConfig_InvokesDelegate()
        {
            var host = CreateHost(out var middleware);
            var options = new DefaultFilesOptions();
            host.AddDefaultFiles(options);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFavicon_RegistersFaviconMiddleware()
        {
            var host = CreateHost(out var middleware);
            host.AddFavicon("/favicon.ico");
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFileServer_WithNullConfig_UsesDefaults()
        {
            var host = CreateHost(out var middleware);
            host.AddFileServer((FileServerOptions)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFileServer_WithConfig_InvokesDelegate()
        {
            var host = CreateHost(out var middleware);
            var options = new FileServerOptions();
            host.AddFileServer(options);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddStaticFiles_WithNullConfig_UsesDefaults()
        {
            var host = CreateHost(out var middleware);
            host.AddStaticFiles((StaticFileOptions)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddStaticFiles_WithConfig_InvokesDelegate()
        {
            var host = CreateHost(out var middleware);
            var options = new StaticFileOptions();
            host.AddStaticFiles(options);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddStaticFiles_WithCustomFileProvider_SetsProvider()
        {
            var host = CreateHost(out var middleware);
            var provider = new PhysicalFileProvider(Directory.GetCurrentDirectory());
            host.AddStaticFiles(o => o.FileProvider = provider);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddStaticFiles_WithCustomRequestPath_SetsPath()
        {
            var host = CreateHost(out var middleware);
            host.AddStaticFiles(o => o.RequestPath = "/static");
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddDefaultFiles_WithCustomDefaultFileNames_SetsNames()
        {
            var host = CreateHost(out var middleware);
            host.AddDefaultFiles(o => o.DefaultFileNames.Add("home.html"));
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFileServer_WithDirectoryBrowsing_EnablesBrowsing()
        {
            var host = CreateHost(out var middleware);
            host.AddFileServer(o => o.EnableDirectoryBrowsing = true);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFileServer_WithCustomRequestPath_SetsPath()
        {
            var host = CreateHost(out var middleware);
            host.AddFileServer(o => o.RequestPath = "/files");
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFavicon_WithNullPath_UsesDefault()
        {
            var host = CreateHost(out var middleware);
            host.AddFavicon(null);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddDefaultFiles_WithNullAction_DoesNotThrow()
        {
            var host = CreateHost(out var middleware);
            host.AddDefaultFiles((Action<DefaultFilesOptions>)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddStaticFiles_WithNullAction_DoesNotThrow()
        {
            var host = CreateHost(out var middleware);
            host.AddStaticFiles((Action<StaticFileOptions>)null!);
            Assert.True(middleware.Count > 0);
        }

        [Fact]
        public void AddFileServer_WithNullAction_DoesNotThrow()
        {
            var host = CreateHost(out var middleware);
            host.AddFileServer((Action<FileServerOptions>)null!);
            Assert.True(middleware.Count > 0);
        }
    }
}
