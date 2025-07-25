// Extension methods for KestrunHost must be in a static class

using Kestrun;
using Microsoft.AspNetCore.Authentication;

namespace Kestrun.Authentication;
public static class AuthenticationExtensions
{
    public static KestrunHost AddAuthentication(this KestrunHost host, string defaultScheme, Action<AuthenticationBuilder> buildPolicy)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(buildPolicy);

        // ① Add authentication services via DI
        host.AddService(services =>
        {
            var builder = services.AddAuthentication(defaultScheme);
            buildPolicy(builder);  // ⬅️ Now you apply the user-supplied schemes here
        });

        // ② Add middleware to enable auth pipeline
        return host.Use(app =>
        {
            app.UseAuthentication();
            app.UseAuthorization(); // optional but useful
        });
    }
}