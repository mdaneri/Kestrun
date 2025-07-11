using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using System.IO;
using System.Collections;
using System.Net;

namespace KestrelLib
{
    public class KestrelServer
    {
        private readonly ConcurrentDictionary<string, string> sharedState = new();
        private readonly WebApplicationBuilder builder;
        private WebApplication? _webApplication;

        private readonly Dictionary<string, object> _kestrelOptions;

        private bool _isConfigured = false;

        /// <summary>
        public WebApplication WebApplication
        {
            get
            {
                if (_webApplication != null)
                    return _webApplication;
                if (builder is null)
                    throw new InvalidOperationException("WebApplicationBuilder is not initialized. Call ConfigureListener first.");
                _webApplication = builder.Build();
                return _webApplication;
            }
        }

        public KestrelServer()
        {
            builder = WebApplication.CreateBuilder();
            _kestrelOptions = [];

            // Disable Kestrel's built-in console lifetime management
            builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();
        }


        public void ConfigureKestrel(Hashtable options)
        {
            foreach (DictionaryEntry entry in options)
            {
                if (entry.Key != null)
                {
                    var keyString = entry.Key.ToString();
                    if (keyString != null)
                    {
                        if (entry.Value != null)
                        {
                            _kestrelOptions[keyString] = entry.Value;
                        }
                    }
                }
            }
            // You can also copy endpoints, pipes, sockets, etc. if set on the options object
            // Or use options.ConfigurationLoader, etc.
        }



        private List<ListenerOptions>? _listenerOptions;

        public void ConfigureListener(int port, IPAddress? iPAddress = null, string? certPath = null, string? certPassword = null, HttpProtocols protocols = HttpProtocols.Http1AndHttp2, bool useConnectionLogging = false)
        {
            if (_listenerOptions == null)
            {
                _listenerOptions = [];
            }
            _listenerOptions.Add(new ListenerOptions
            {
                IPAddress = iPAddress ?? IPAddress.Any,
                Port = port,
                UseHttps = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword),
                CertPath = certPath,
                CertPassword = certPassword,
                Protocols = protocols,
                UseConnectionLogging = useConnectionLogging
            });

        }

        public void AddRoute(string pattern, string scriptBlock, string httpMethod = "GET")
        {
            try
            {
                var app = WebApplication;

                // ApplyConfiguration();

                app.MapMethods(pattern, [httpMethod.ToUpperInvariant()], async (HttpContext context) =>
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync();

                    var request = new
                    {
                        context.Request.Method,
                        Path = context.Request.Path.ToString(),
                        Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                        Headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString()),
                        Body = body
                    };

                    var inputJson = JsonSerializer.Serialize(request);

                    using PowerShell ps = PowerShell.Create();
                    using var rs = RunspaceFactory.CreateRunspace();
                    rs.Open();
                    // rs.SessionStateProxy.SetVariable("RequestJson", inputJson);
                    rs.SessionStateProxy.SetVariable("Request", request);
                    ps.Runspace = rs;
                    ps.AddScript(scriptBlock);

                    var results = await Task.Run(() => ps.Invoke());
                    if (ps.HadErrors)
                    {
                        context.Response.StatusCode = 500;
                        return "❌ PowerShell error";
                    }
                    return string.Join("\n", results.Select(r => r.ToString()));
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add route '{pattern}' with method '{httpMethod}': {ex.Message}", ex);
            }
        }

        public void AddRoute(string pattern)
        {
            WebApplication.Map(pattern, async (HttpContext context) =>
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();

                var request = new
                {
                    Method = context.Request.Method,
                    Path = context.Request.Path.ToString(),
                    Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                    Headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString()),
                    Body = body
                };

                var inputJson = JsonSerializer.Serialize(request);

                using PowerShell ps = PowerShell.Create();
                using var rs = RunspaceFactory.CreateRunspace();
                rs.Open();
                rs.SessionStateProxy.SetVariable("RequestJson", inputJson);
                rs.SessionStateProxy.SetVariable("SharedHash", sharedState);
                ps.Runspace = rs;

                ps.AddScript(@"$data = $RequestJson | ConvertFrom-Json
$key = $data.Path.Trim('/')
$SharedHash[$key] = $data.Body
""Stored '$($data.Body)' under key '$key'""");

                var results = await Task.Run(() => ps.Invoke());

                if (ps.HadErrors)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("❌ PowerShell error");
                    return;
                }

                var output = string.Join("\n", results.Select(r => r.ToString()));
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync(output);
            });
        }


        public void ApplyConfiguration()
        {
            if (_isConfigured)
            {
                return; // Already configured
            }
            if (_kestrelOptions.Count == 0)
            {
                throw new InvalidOperationException("No Kestrel options configured. Call ConfigureKestrel first.");
            }



            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.

            builder.WebHost.ConfigureKestrel(options =>
         {
             if (_kestrelOptions.TryGetValue("AllowSynchronousIO", out object? allowSyncIOValue) && allowSyncIOValue is bool)
             {
                 options.AllowSynchronousIO = (bool)(allowSyncIOValue ?? false);
             }
             if (_kestrelOptions.TryGetValue("AllowResponseHeaderCompression", out object? allowRespHeaderCompValue) && allowRespHeaderCompValue is bool)
             {
                 options.AllowResponseHeaderCompression = (bool)(allowRespHeaderCompValue ?? false);
             }
             if (_kestrelOptions.TryGetValue("AddServerHeader", out object? addServerHeaderValue) && addServerHeaderValue is bool)
             {
                 options.AddServerHeader = (bool)(addServerHeaderValue ?? false);
             }
             if (_kestrelOptions.TryGetValue("AllowHostHeaderOverride", out object? allowHostHeaderOverrideValue) && allowHostHeaderOverrideValue is bool)
             {
                 options.AllowHostHeaderOverride = (bool)(allowHostHeaderOverrideValue ?? false);
             }
             // Copy all settings from the provided _kestrelOptions
             if (_kestrelOptions.TryGetValue("AllowAlternateSchemes", out object? allowAlternateSchemesValue) && allowAlternateSchemesValue is bool)
             {
                 options.AllowAlternateSchemes = (bool)(allowAlternateSchemesValue ?? false);
             }
             if (_kestrelOptions.TryGetValue("DisableStringReuse", out object? disableStringReuseValue) && disableStringReuseValue is bool)
             {
                 options.DisableStringReuse = (bool)(disableStringReuseValue ?? false);
             }
             if (_kestrelOptions.TryGetValue("ResponseHeaderEncodingSelector", out object? respHeaderEncodingSelectorValue) && respHeaderEncodingSelectorValue is Func<string, Encoding?>)
             {
                 if (_kestrelOptions["ResponseHeaderEncodingSelector"] is Func<string, Encoding?> selector)
                 {
                     options.ResponseHeaderEncodingSelector = selector;
                 }
                 // Optionally, handle other types or log a warning if the type is incorrect.
             }
             if (_kestrelOptions.TryGetValue("RequestHeaderEncodingSelector", out object? reqHeaderEncodingSelectorValue) && reqHeaderEncodingSelectorValue is Func<string, Encoding?>)
             {
                 if (_kestrelOptions["RequestHeaderEncodingSelector"] is Func<string, Encoding?> selector)
                 {
                     options.RequestHeaderEncodingSelector = selector;
                 }
             }
             if (_kestrelOptions.TryGetValue("Limits", out object? limitsValue) && limitsValue is Hashtable limitsTable && limitsTable.Count > 0)
             {
                 // If the _kestrelOptions contains a KestrelServerLimits object, copy it    
                 if (limitsTable.ContainsKey("MaxRequestBodySize") && limitsTable["MaxRequestBodySize"] != null && limitsTable["MaxRequestBodySize"] is long maxRequestBodySize)
                 {
                     options.Limits.MaxRequestBodySize = maxRequestBodySize;
                 }

                 if (limitsTable.ContainsKey("MaxConcurrentConnections") && limitsTable["MaxConcurrentConnections"] != null && limitsTable["MaxConcurrentConnections"] is long maxConcurrentConnections)
                 {
                     options.Limits.MaxConcurrentConnections = maxConcurrentConnections;
                 }

                 if (limitsTable.ContainsKey("MaxRequestHeaderCount") && limitsTable["MaxRequestHeaderCount"] != null && limitsTable["MaxRequestHeaderCount"] is int maxRequestHeaderCount)
                 {
                     options.Limits.MaxRequestHeaderCount = maxRequestHeaderCount;
                 }
                 if (limitsTable.ContainsKey("KeepAliveTimeout") && limitsTable["KeepAliveTimeout"] != null && limitsTable["KeepAliveTimeout"] is TimeSpan keepAliveTimeout)
                 {
                     options.Limits.KeepAliveTimeout = keepAliveTimeout;
                 }
             }
             if (_listenerOptions != null && _listenerOptions.Count > 0)
             {
                 _listenerOptions.ForEach(opt =>
                              {

                                  options.Listen(opt.IPAddress, opt.Port, listenOptions =>
                                  {
                                      listenOptions.Protocols = opt.Protocols;

                                      if (opt.UseHttps && !string.IsNullOrEmpty(opt.CertPath))
                                      {
                                          listenOptions.UseHttps(opt.CertPath, opt.CertPassword);
                                      }

                                      if (opt.UseConnectionLogging)
                                      {
                                          listenOptions.UseConnectionLogging();
                                      }
                                  });

                              });
             }

         });
            _isConfigured = true;
        }
        public void Run()
        {
            ApplyConfiguration();
            WebApplication.Run();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ApplyConfiguration();
            await WebApplication.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_webApplication != null)
            {
                await _webApplication.StopAsync(cancellationToken);
            }
        }

        public void Stop()
        {
            if (_webApplication != null)
            {
                // This initiates a graceful shutdown.
                _webApplication.Lifetime.StopApplication();
            }
        }
    }
}
