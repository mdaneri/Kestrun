using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Security.Claims;
using System.Threading.Tasks;
using Kestrun.Authentication;
using Kestrun.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp;
using Moq;
using Serilog;
using Xunit;

namespace Kestrun.Authentication.Tests
{
    public class IAuthHandlerTest
    {
        [Fact]
        public async Task GetAuthenticationTicketAsync_AddsNameClaim_WhenMissing()
        {
            // Arrange
            var context = new DefaultHttpContext();
            var loggerMock = new Mock<ILogger>();
            var optionsMock = new Mock<IAuthenticationCommonOptions>();
            optionsMock.SetupGet(o => o.IssueClaims).Returns((Func<HttpContext, string, Task<IEnumerable<Claim>>>)null!);
            optionsMock.SetupGet(o => o.Logger).Returns(loggerMock.Object);

            var scheme = new AuthenticationScheme("TestScheme", null, typeof(Kestrun.Authentication.BasicAuthHandler));
            var user = "testuser";

            // Act
            var ticket = await IAuthHandler.GetAuthenticationTicketAsync(context, user, optionsMock.Object, scheme);

            // Assert
            Assert.NotNull(ticket);
            Assert.Equal("TestScheme", ticket.AuthenticationScheme);
            var nameClaim = ticket.Principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            Assert.NotNull(nameClaim);
            Assert.Equal(user, nameClaim.Value);
        }

        [Fact]
        public async Task ValidatePowerShellAsync_ReturnsFalse_WhenCredentialsAreNull()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Items["PS_INSTANCE"] = PowerShell.Create();
            var loggerMock = new Mock<ILogger>();
            var result = await IAuthHandler.ValidatePowerShellAsync("return $true", context, [], loggerMock.Object);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task ValidatePowerShellAsync_ReturnsFalse_WhenCodeIsNull()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Items["PS_INSTANCE"] = PowerShell.Create();
            var loggerMock = new Mock<ILogger>();
            var credentials = new Dictionary<string, string> { { "username", "u" }, { "password", "p" } };

            var result = await IAuthHandler.ValidatePowerShellAsync(null, context, credentials, loggerMock.Object);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IssueClaimsPowerShellAsync_ReturnsEmpty_WhenIdentityIsNull()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Items["PS_INSTANCE"] = PowerShell.Create();
            var loggerMock = new Mock<ILogger>();

            var claims = await IAuthHandler.IssueClaimsPowerShellAsync("return $true", context, "", loggerMock.Object);

            Assert.Empty(claims);
        }

        [Fact]
        public void BuildCsIssueClaims_ReturnsFunc()
        {
            // Arrange
            var settings = new AuthenticationCodeSettings
            {
                Code = "return (System.Collections.Generic.IEnumerable<System.Security.Claims.Claim>) new System.Collections.Generic.List<System.Security.Claims.Claim>() { new System.Security.Claims.Claim(\"role\", \"admin\") };",
                CSharpVersion = Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest,
                ExtraImports = new[] { "System", "System.Linq", "System.Collections.Generic", "System.Security.Claims", "Kestrun", "Microsoft.AspNetCore.Http" }
            };
            var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

            // Act
            var func = IAuthHandler.BuildCsIssueClaims(settings, logger);

            // Assert
            Assert.NotNull(func);
        }

        [Fact]
        public void BuildVBNetIssueClaims_ReturnsFunc()
        {
            // Arrange
            var settings = new AuthenticationCodeSettings
            {
                Code = "Return New System.Collections.Generic.List(Of System.Security.Claims.Claim)() From { New System.Security.Claims.Claim(\"role\", \"admin\") }",
                VisualBasicVersion = Microsoft.CodeAnalysis.VisualBasic.LanguageVersion.Latest
            };
            var loggerMock = new Mock<ILogger>();

            // Act
            var func = IAuthHandler.BuildVBNetIssueClaims(settings, loggerMock.Object);

            // Assert
            Assert.NotNull(func);
        }
    }
}