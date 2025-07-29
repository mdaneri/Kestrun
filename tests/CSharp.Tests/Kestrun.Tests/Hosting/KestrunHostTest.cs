using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Xunit;
using Serilog;
using Serilog.Events;
using Kestrun;
using Kestrun.Utilities;
// Add the following using if MapRouteOptions is in another namespace
using Kestrun.Hosting;

namespace KestrunTests.Hosting;

public class KestrunHostTest
{
    [Fact]
    public void Constructor_SetsApplicationName_WhenProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        Assert.Equal("TestApp", host.ApplicationName);
    }

    [Fact]
    public void Constructor_UsesDefaultApplicationName_WhenNotProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost(null, logger);

        Assert.Equal("KestrunApp", host.ApplicationName);
    }

    [Fact]
    public void Constructor_SetsKestrunRoot_WhenProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var tempDir = Path.GetTempPath();
        var host = new KestrunHost("TestApp", logger, tempDir);

        Assert.Equal(tempDir, host.KestrunRoot);
    }

    [Fact]
    public void ConfigureListener_AddsListenerOptions()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        host.ConfigureListener(8080, IPAddress.Loopback, false);

        Assert.Contains(host.Options.Listeners, l => l.Port == 8080 && l.IPAddress.Equals(IPAddress.Loopback));
    }

    [Fact]
    public void ConfigureListener_UsesDefaultIPAddress_WhenNotProvided()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        host.ConfigureListener(8081, false);

        Assert.Contains(host.Options.Listeners, l => l.Port == 8081 && l.IPAddress.Equals(IPAddress.Any));
    }

    [Fact]
    public void AddNativeRoute_ThrowsIfAppNotInitialized()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        Assert.Throws<InvalidOperationException>(() =>
           host.AddNativeRoute("/test", HttpVerb.Get, async ctx => { await System.Threading.Tasks.Task.CompletedTask; })
        );
    }

    [Fact]
    public void AddMapRoute_ThrowsIfAppNotInitialized()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        var options = new MapRouteOptions
        {
            Pattern = "/test",
            HttpVerbs = new[] { HttpVerb.Get },
            Code = "Write-Output 'Hello'",
            Language = ScriptLanguage.PowerShell
        };

        _ = Assert.Throws<InvalidOperationException>(() =>
            host.AddMapRoute(options)
        );
    }

    [Fact]
    public void AddHtmlTemplateRoute_ThrowsIfFileNotFound()
    {
        var logger = new LoggerConfiguration().CreateLogger();
        var host = new KestrunHost("TestApp", logger);

        var options = new MapRouteOptions
        {
            Pattern = "/html",
            HttpVerbs = new[] { HttpVerb.Get }
        };

        Assert.Throws<FileNotFoundException>(() =>
            host.AddHtmlTemplateRoute(options, "nonexistent.html")
        );
    }
    
     [Fact]
    public void IsCSharpScriptValid_ReturnsTrueForValid()
    {
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        bool valid = host.IsCSharpScriptValid("System.Console.WriteLine(\"hi\");");
        Assert.True(valid);
    }
    [Fact]
    public void IsCSharpScriptValid_ReturnsFalseForInvalid()
    {
        KestrunHostManager.SetKestrunRoot(AppContext.BaseDirectory);
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        bool valid = host.IsCSharpScriptValid("System.Console.Writeline(\"hi\");");
        Assert.False(valid);
    }
    [Fact]
    public void GetCSharpScriptErrors_ReturnsMessage()
    {
        KestrunHostManager.SetKestrunRoot(AppContext.BaseDirectory);
        var host = new KestrunHost("TestHost", AppContext.BaseDirectory);
        var msg = host.GetCSharpScriptErrors("System.Console.Writeline(\"hi\");");
        Assert.NotNull(msg);
    }
}
