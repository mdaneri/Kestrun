using System.Security.Claims;
using Kestrun.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Xunit;

namespace KestrunTests.Claims;

public class ClaimPolicyExtensionsTests
{
    [Fact]
    public void ToAuthzDelegate_Adds_All_Policies_With_Correct_Requirements()
    {
        var cfg = new ClaimPolicyConfig
        {
            Policies = new()
            {
                ["Admin"] = new ClaimRule(ClaimTypes.Role, "Admin", "Owner"),
                ["Named"] = new ClaimRule(ClaimTypes.Name, "Alice")
            }
        };

        var options = new AuthorizationOptions();
        var del = cfg.ToAuthzDelegate();
        del(options);

        var p1 = options.GetPolicy("Admin");
        Assert.NotNull(p1);
        var req1 = Assert.IsType<ClaimsAuthorizationRequirement>(p1!.Requirements.Single());
        Assert.Equal(ClaimTypes.Role, req1.ClaimType);
        Assert.Equal(["Admin", "Owner"], req1.AllowedValues!.ToArray());

        var p2 = options.GetPolicy("Named");
        Assert.NotNull(p2);
        var req2 = Assert.IsType<ClaimsAuthorizationRequirement>(p2!.Requirements.Single());
        Assert.Equal(ClaimTypes.Name, req2.ClaimType);
        Assert.Equal(["Alice"], req2.AllowedValues!.ToArray());
    }

    [Fact]
    public void ToAuthzDelegate_With_Empty_Config_Adds_No_Policies()
    {
        var cfg = new ClaimPolicyConfig();
        var options = new AuthorizationOptions();

        cfg.ToAuthzDelegate()(options);

        // No named policies expected; lookup for any name should be null
        Assert.Null(options.GetPolicy("AnyName"));
    }
}
