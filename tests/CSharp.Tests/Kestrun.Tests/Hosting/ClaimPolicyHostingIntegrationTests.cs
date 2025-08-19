using System.Linq;
using System.Security.Claims;
using Kestrun.Authentication;
using Kestrun.Claims;
using Kestrun.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrunTests.Hosting;

public class ClaimPolicyHostingIntegrationTests
{
    [Fact]
    public async Task BasicAuth_Adds_Policies_Via_PostConfigurer()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["MustBeAdmin"] = new ClaimRule(ClaimTypes.Role, "Admin"),
                ["NamedAlice"] = new ClaimRule(ClaimTypes.Name, "Alice")
            }
        };

        host.AddBasicAuthentication(
            scheme: "BasicX",
            configure: opts =>
            {
                opts.RequireHttps = false; // keep defaults simple
                opts.ClaimPolicyConfig = cfg;
            });

        var app = host.Build();

        Assert.True(host.HasAuthScheme("BasicX"));
        Assert.True(host.HasAuthPolicy("MustBeAdmin"));
        Assert.True(host.HasAuthPolicy("NamedAlice"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("MustBeAdmin");
        Assert.NotNull(policy);
        var req = Assert.IsType<ClaimsAuthorizationRequirement>(policy!.Requirements.Single());
        Assert.Equal(ClaimTypes.Role, req.ClaimType);
        Assert.Equal(new[] { "Admin" }, req.AllowedValues!.ToArray());
    }

    [Fact]
    public async Task ApiKeyAuth_Adds_Policies_Via_PostConfigurer()
    {
        var host = new KestrunHost("TestApp");

        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["AllowUserBob"] = new ClaimRule(ClaimTypes.Name, "Bob")
            }
        };

        host.AddApiKeyAuthentication(
            scheme: "ApiKeyX",
            configure: opts =>
            {
                opts.RequireHttps = false;
                opts.ExpectedKey = "ignored-for-test";
                opts.ClaimPolicyConfig = cfg;
            });

        var app = host.Build();

        Assert.True(host.HasAuthScheme("ApiKeyX"));
        Assert.True(host.HasAuthPolicy("AllowUserBob"));

        var policyProvider = app.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync("AllowUserBob");
        Assert.NotNull(policy);
        var req = Assert.IsType<ClaimsAuthorizationRequirement>(policy!.Requirements.Single());
        Assert.Equal(ClaimTypes.Name, req.ClaimType);
        Assert.Equal(new[] { "Bob" }, req.AllowedValues!.ToArray());
    }
}
