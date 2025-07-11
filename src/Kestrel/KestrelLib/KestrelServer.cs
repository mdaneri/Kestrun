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
        private WebApplication? App;

        private readonly Dictionary<string, object> _kestrelOptions;
        private readonly List<string> _modulePaths = new();

        private bool _isConfigured = false;



        // Accepts optional module paths (from PowerShell)
        public KestrelServer(string? appName = null, object? modulePathsObj = null)
        {
            builder = WebApplication.CreateBuilder();
            _kestrelOptions = [];
            if (!string.IsNullOrEmpty(appName))
            {
                _kestrelOptions["ApplicationName"] = appName;
            }
            // Store module paths if provided
            if (modulePathsObj is IEnumerable<object> modulePathsEnum)
            {
                foreach (var modPathObj in modulePathsEnum)
                {
                    if (modPathObj is string modPath && !string.IsNullOrWhiteSpace(modPath))
                    {
                        _modulePaths.Add(modPath);
                    }
                }
            }
            /*   builder.Services.AddResponseCompression(options =>
               {
                   options.EnableForHttps = true;
               });*/
        }


        private void KestrelServices(WebApplicationBuilder builder)
        {
            builder = builder ?? throw new ArgumentNullException(nameof(builder));

            // Disable Kestrel's built-in console lifetime management
            builder.Services.AddSingleton<IHostLifetime, NoopHostLifetime>();
            try
            {
                builder.Services.AddResponseCompression(options =>
                {
                    options.EnableForHttps = true;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to configure response compression.", ex);
            }
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

        public void ConfigureListener(int port, IPAddress? iPAddress = null, string? certPath = null, string? certPassword = null, HttpProtocols protocols = HttpProtocols.Http1, bool useConnectionLogging = false)
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
                if (App == null)
                {
                    throw new InvalidOperationException("WebApplication is not initialized. Call ApplyConfiguration first.");
                }

                _ = App.MapMethods(pattern, [httpMethod.ToUpperInvariant()], async (HttpContext context) =>
                {
                    using var reader = new StreamReader(context.Request.Body);
                    var body = await reader.ReadToEndAsync();
                   // context.Request.Headers.Remove("Accept-Encoding");
                    var request = new
                    {
                        context.Request.Method,
                        Path = context.Request.Path.ToString(),
                        Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                        Headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString()),
                        Body = body
                    };

                    var inputJson = JsonSerializer.Serialize(request);

                    var KrResponse = new KestrunResponse();

                    using PowerShell ps = PowerShell.Create();
                    using var rs = RunspaceFactory.CreateRunspace();
                    rs.Open();
                    rs.SessionStateProxy.SetVariable("Request", request);
                    rs.SessionStateProxy.SetVariable("Response", KrResponse); 
                    ps.Runspace = rs;

                    // Always import stored modules
                    foreach (var modPath in _modulePaths)
                    {
                        if (!string.IsNullOrWhiteSpace(modPath))
                        {
                            ps.AddScript($"Import-Module -Name '{modPath.Replace("'", "''")}' -ErrorAction SilentlyContinue").Invoke();
                            ps.Commands.Clear();
                        }
                    }

                    ps.AddScript(scriptBlock);

                    var results = await Task.Run(() => ps.Invoke());
                    
                     // Capture errors and output from the runspace
                    var errorOutput = ps.Streams.Error.Select(e => e.ToString()).ToList();
                    var verboseOutput = ps.Streams.Verbose.Select(v => v.ToString()).ToList();
                    var warningOutput = ps.Streams.Warning.Select(w => w.ToString()).ToList();
                    var debugOutput = ps.Streams.Debug.Select(d => d.ToString()).ToList();
                    var infoOutput = ps.Streams.Information.Select(i => i.ToString()).ToList();

                    if (ps.HadErrors || errorOutput.Count > 0)
                    {
                        context.Response.StatusCode = 500;
                        var errorMsg = $"❌[Error]\n\t" + string.Join("\n\t", errorOutput); 
                        if (verboseOutput.Count > 0)
                            errorMsg += "\n💬[Verbose]\n\t" + string.Join("\n\t", verboseOutput);
                        if (warningOutput.Count > 0)
                            errorMsg += "\n⚠️[Warning]\n\t" + string.Join("\n\t", warningOutput);
                        if (debugOutput.Count > 0)
                            errorMsg += "\n🐞[Debug]\n\t" + string.Join("\n\t", debugOutput);
                        if (infoOutput.Count > 0)
                            errorMsg += "\nℹ️[Info]\n\t" + string.Join("\n\t", infoOutput);
                        Console.WriteLine(errorMsg);
                        return errorMsg;
                    }


                    // If redirect, nothing to return
                    if (!string.IsNullOrEmpty(KrResponse.RedirectUrl))
                        return string.Empty;
                    await KrResponse.ApplyTo(context.Response);
                    // Optionally, you could return output/verbose/debug info here for diagnostics
                    // return string.Join("\n", results.Select(r => r.ToString()));
                      return string.Empty;
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add route '{pattern}' with method '{httpMethod}': {ex.Message}", ex);
            }
        }



        public void ApplyConfiguration()
        {
            if (_isConfigured)
            {
                return; // Already configured
            }
            /*    if (_kestrelOptions.Count == 0)
                {
                    throw new InvalidOperationException("No Kestrel options configured. Call ConfigureKestrel first.");
                }
    */
            // This method is called to apply the configured options to the Kestrel server.
            // The actual application of options is done in the Run method.


            KestrelServices(builder);

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

             /*if (_kestrelOptions.TryGetValue("ApplicationName", out object? appName) && appName is string appNameString)
             {
                 options.ApplicationName = appNameString;
             }*/
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
            App = builder.Build();
            App.UseResponseCompression();

            _isConfigured = true;
        }
        public void Run()
        {
            ApplyConfiguration();
            App?.Run();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ApplyConfiguration();
            if (App != null)
            {
                await App.StartAsync(cancellationToken);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (App != null)
            {
                await App.StopAsync(cancellationToken);
            }
        }

        public void Stop()
        {
            // This initiates a graceful shutdown.
            App?.Lifetime.StopApplication();
        }
    }
}
