using System.Collections;
using System.Reflection;
using System.Text.Json;
using Kestrun.Hosting;
using Kestrun.SharedState;

namespace Kestrun.Utilities;

public static class VariablesMap
{
    public static bool GetVariablesMap(KestrunContext ctx, ref Dictionary<string, object?> vars)
    {
        // ① Initialize the dictionary
        vars ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        return GetCommonProperties(ctx, ref vars) // ② Add common request properties
        && GetSharedStateStore(ref vars); // ③ Add shared state variables
    }

    public static bool GetSharedStateStore(ref Dictionary<string, object?> vars)
    {
        vars ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in SharedStateStore.Snapshot())
            FlattenInto(vars, kv.Key, kv.Value);

        return true;
    }

    public static bool GetCommonProperties(KestrunContext ctx, ref Dictionary<string, object?> vars)
    {
        // ① Initialize the dictionary

        vars["Request.Path"] = ctx.Request.Path;
        vars["Request.Method"] = ctx.Request.Method;
        vars["QueryString"] = ctx.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
        vars["Form"] = ctx.Request.Form;
        vars["Cookies"] = ctx.Request.Cookies;
        vars["Headers"] = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        vars["UserAgent"] = ctx.Request.Headers["User-Agent"].ToString();
        //    vars["ServerSoftware"] = "Kestrun/" + Options.ApplicationName;
        vars["ServerVersion"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        vars["ServerOS"] = Environment.OSVersion.ToString();
        vars["ServerArch"] = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        vars["ServerIP"] = ctx.HttpContext.Connection.LocalIpAddress?.ToString() ?? "unknown";
        vars["ServerPort"] = ctx.HttpContext.Connection.LocalPort;
        vars["ServerName"] = Environment.MachineName;
        vars["Timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        return true;
    }

    /// <summary>
    /// If value is IDictionary, serialize it and also add sub-keys;
    /// otherwise just add the single entry.
    /// </summary>
    private static void FlattenInto(
        Dictionary<string, object?> vars,
        string key,
        object? value)
    {
        if (value is IDictionary dict)
        {
            // 1) top-level JSON
            vars[key] = JsonSerializer.Serialize(dict);

            // 2) sub-keys
            foreach (DictionaryEntry entry in dict)
            {
                var subKey = $"{key}.{entry.Key}";
                vars[subKey] = entry.Value;
            }
        }
        else
        {
            // simple scalar or other object
            vars[key] = value;
        }
    }
}

