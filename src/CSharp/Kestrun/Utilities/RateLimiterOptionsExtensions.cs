using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
namespace Kestrun.Utilities;
public static class RateLimiterOptionsExtensions
{
    private static readonly MethodInfo AddPolicyMethod =
        typeof(RateLimiterOptions)
            .GetMethods()
            .Single(m => m.Name == "AddPolicy" && m.GetParameters().Length == 2);

    private static readonly FieldInfo PolicyMapField =
        typeof(RateLimiterOptions).GetField("PolicyMap",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo UnactivatedPolicyMapField =
        typeof(RateLimiterOptions).GetField("UnactivatedPolicyMap",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Copies every policy and the scalar settings from <paramref name="source"/> into <paramref name="target"/>.</summary>
    public static void CopyFrom(this RateLimiterOptions target, RateLimiterOptions source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));

        // ───── scalar props ─────
        target.GlobalLimiter       = source.GlobalLimiter;
        target.OnRejected          = source.OnRejected;
        target.RejectionStatusCode = source.RejectionStatusCode;

        // ───── activated policies ─────
        var policyMap = (IDictionary<string, object>)PolicyMapField.GetValue(source)!;
        foreach (var kvp in policyMap)
        {
            // AddPolicy(string, IRateLimiterPolicy<HttpContext>)
            AddPolicyMethod.Invoke(target, new[] { kvp.Key, kvp.Value });
        }

        // ───── factories awaiting DI (un-activated) ─────
        var factoryMap = (IDictionary<string, object>)UnactivatedPolicyMapField.GetValue(source)!;
        foreach (var kvp in factoryMap)
        {
            // AddPolicy(string, Func<IServiceProvider, IRateLimiterPolicy<HttpContext>>)
            AddPolicyMethod.Invoke(target, new[] { kvp.Key, kvp.Value });
        }
    }
}
