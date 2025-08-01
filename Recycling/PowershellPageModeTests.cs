using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Kestrun;
 
    public class PowerShellRazorPageTests
    {
        [Fact]
        public void PowerShellPageModel_ReturnsData()
        {
            // Arrange: simulate a context with a PageModel value
            var httpContext = new DefaultHttpContext();
            var expected = new { Message = "Hello from PowerShell" };
            httpContext.Items["PageModel"] = expected;

            var pageModel = new PowerShellPageModel
            {
                // Set HttpContext manually (in ASP.NET Core this is normally set at runtime)
                ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext }
            };

            // Act
            dynamic actual = pageModel.Data;

            // Assert
            Assert.NotNull(actual);
            Assert.Equal(expected.Message, (string)actual.Message);
        }

        [Fact]
        public async Task Middleware_ProcessesPowerShellScriptAndSetsPageModel()
        {
            // Arrange: Create temporary Pages folder with TestPage.cshtml and TestPage.cshtml.ps1 files.
            string tempPagesDir = Path.Combine(Path.GetTempPath(), "KestrunTestPages");
            Directory.CreateDirectory(tempPagesDir);

            // Create a dummy Razor view (the content is not used by our middleware)
            string cshtmlPath = Path.Combine(tempPagesDir, "TestPage.cshtml");
            File.WriteAllText(cshtmlPath, "<h1>Test Page</h1>");

            // Create a PS script that sets $Model (which becomes PageModel)
            // In this simple example, the PS script sets $Model to a hashtable with Message property.
            string ps1Path = cshtmlPath + ".ps1";
            File.WriteAllText(ps1Path, @"$Model = @{ Message = 'Hello from PowerShell' }");

            // We'll use a HostBuilder to create an in-memory test server.
            using IHost host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();
                    // Set the content root so that we can map our temporary Pages folder later.
                    webBuilder.UseContentRoot(Path.GetTempPath());
                    webBuilder.Configure(app =>
                    {
                        // Copy our temporary Pages folder into the expected "Pages" directory.
                        string pagesDestination = Path.Combine(app.ApplicationServices.GetService(typeof(IWebHostEnvironment)) is IWebHostEnvironment env 
                            ? env.ContentRootPath 
                            : throw new InvalidOperationException(), 
                            "Pages");
                        if (Directory.Exists(pagesDestination))
                        {
                            Directory.Delete(pagesDestination, recursive: true);
                        }
                        DirectoryCopy(tempPagesDir, pagesDestination, copySubDirs: true);

                        // Add our custom middleware that runs the PS script.
                        // Note: KestrunRunspacePoolManager should be provided; here we assume a parameterless constructor.
                        app.UsePowerShellRazorPages(new KestrunRunspacePoolManager());

                        // Add an endpoint to return the PageModel content if it exists.
                        app.Run(async context =>
                        {
                            if (context.Items.ContainsKey("PageModel"))
                            {
                                // In production, the Razor view would use the model.
                                // For testing, simply return one property.
                                dynamic model = context.Items["PageModel"];
                                await context.Response.WriteAsync((string)model.Message);
                            }
                            else
                            {
                                await context.Response.WriteAsync("No Model Set");
                            }
                        });
                    });
                })
                .StartAsync();

            HttpClient client = host.GetTestClient();
            HttpResponseMessage response = await client.GetAsync("/TestPage");
            string responseText = await response.Content.ReadAsStringAsync();

            // Clean up temporary directory.
            Directory.Delete(tempPagesDir, recursive: true);

            // Assert that the PowerShell script ran and set the model.
            Assert.Contains("Hello from PowerShell", responseText);
        }

        // Helper method: Recursively copy a directory.
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException("Source directory not found: " + sourceDirName);
            }

            Directory.CreateDirectory(destDirName);
            foreach (FileInfo file in dir.GetFiles())
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dir.GetDirectories())
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }
    }
 