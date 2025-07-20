# PowerShell-backed Razor Pages

> *Dynamic ASP.NET Core UI powered by PowerShell scripts — all inside **Kestrun***

---

## 1. Overview

Kestrun lets you pair a regular **`.cshtml`** Razor view with a sibling **PowerShell script**.
During a single HTTP request the pipeline looks like this:

```text
┌───────────────┐
│  Browser      │  GET /Hello
└────┬──────────┘
     │
┌────▼──────────┐
│ PS-Razor MW   │  ① runs Hello.cshtml.ps1
│ (UsePower… )  │     – builds $Model
└────┬──────────┘
     │  sets HttpContext.Items["PageModel"]
┌────▼──────────┐
│ Razor engine  │  ② renders Hello.cshtml
│ (MapRazorPages)│    – @model PowerShellPageModel
└────┬──────────┘
     │
┌────▼──────────┐
│   Response    │  HTML sent back
└───────────────┘
```

*Advantages*

* **Zero compile step** — change the `.ps1` file, hit *F5*, refresh.
* **Full access to Kestrun abstractions** (`$Request`, `$Response`, loggers, DI services).
* **Razor tooling** — syntax highlighting, IntelliSense, TagHelpers, layout views, etc.

---

## 2. Folder & naming convention

```
MyApp/
└─ Pages/
   ├─ Hello.cshtml         ← Razor markup
   └─ Hello.cshtml.ps1     ← PowerShell executed first
   ├─ Admin/
   │  ├─ Index.cshtml
   │  └─ Index.cshtml.ps1
   └─ _Layout.cshtml       ← optional shared layout
```

* URL rule: `/Pages/Hello.cshtml` → **`/Hello`**
* Sub-folders map to path segments (`/Admin/Index` → `/Admin`).

---

## 3. Enabling the middleware

```csharp
var server = new KestrunHost("MySite", kestRunRoot, [modulePath]);

server.ConfigureKestrel(opts => { /* … */ });

server.ApplyConfiguration();   // wires middleware

/*
  Inside ApplyConfiguration():
    app.UseStaticFiles();
    app.UsePowerShellRazorPages(runspacePool);  // 👈 middleware
    app.UseRouting();
    app.MapRazorPages();                        // standard Razor
*/
```

> **Important:** `UsePowerShellRazorPages()` **must appear before** `MapRazorPages()` so that the `$Model` object is ready when Razor runs.

---

## 4. Writing your first page

### 4.1 `Pages/Hello.cshtml`

```razor
@page
@model Kestrun.PowerShellPageModel   <!-- supplied by Kestrun -->

@{
    Layout = null;                   // use a plain page here
    var data = Model.Data            // dynamic object from PS
              ?? new { Title = "Fallback", UserName = "Guest" };
}

<!DOCTYPE html>
<h1>@data.Title</h1>
<p>Welcome, @data.UserName!</p>
<p>Served at @DateTime.UtcNow:u</p>
```

### 4.2 `Pages/Hello.cshtml.ps1`

```powershell
<# Executed **in the same request** before Razor renders #>

# Build any object you like –
$Model = [pscustomobject]@{
    Title    = 'PowerShell-backed Razor Page'
    UserName = 'Alice'
}

# Optional helpers:
#   $Request   - KestrunRequest wrapper
#   $Response  - KestrunResponse helper
#   $Log       - current ILogger (Serilog)
#   $Services  - IServiceProvider (DI container)
```

---

## 5. What variables are available in the script?

| Name            | Type                     | Purpose                                               |
|-----------------|--------------------------|-------------------------------------------------------|
| **`$Request`**  | `KestrunRequest`         | Strong-typed wrapper over `HttpRequest` with helpers. |
| **`$Response`** | `KestrunResponse`        | Convenience builder (status, headers, cookies…).      |
| **`$Services`** | `IServiceProvider`       | Resolve any DI singleton/scoped service.              |
| **`$Log`**      | `Serilog.ILogger`        | Logger scoped to the current request.                 |
| **`$Model`**    | `object` (you create it) | Anything serialisable / anonymous / PSCustomObject.   |

Return values are ignored; simply assign to `$Model`.

---

## 6. Advanced examples

### 6.1 Query a database and cache

```powershell
param($id)   # accepts route & query parameters

$cache = $Services.GetService([IMemoryCache])
if (-not $cache.TryGetValue($id, [ref]$Model)) {
    $db = $Services.GetService([MyApp.Data.PersonRepository])
    $Model = $db.GetPerson($id)
    $cache.Set($id, $Model, [TimeSpan]::FromMinutes(10))
}

$Log.Information("Served person {Id}", $id)
```

### 6.2 Custom 404

```powershell
if (-not (Test-Path "data/$($Request.RouteValues.id).json")) {
    $Response.Status(404)
             .WriteText("No such record")
    return   # skip Razor entirely
}
```

---

## 7. Tips & best practices

* **Strong typing helps** – cast `$Model` to a real .NET class for IntelliSense in Razor (`@model Person`).
* **Keep business logic out of `.ps1`** – call C# services from DI instead.
* **One script = one request** – avoid long-running background work; offload to hosted services.
* **Case matters on Linux** – name files and hit URLs with matching case.
  `Hello.cshtml` → `/Hello` (not `/hello`) if deploying to Linux containers.
* **Hot reload** – edit `.ps1` or `.cshtml`, save, refresh; no rebuild required.
* **Logging** – use `$Log` or the `[Serilog.Log]` static to record diagnostics.
  (See **Logging.md** for full details.)

---

## 8. FAQ

| Question                                    | Answer                                                                                                                                   |
|---------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| *Can I share code between scripts?*         | Yes. Using the SharedState feature                                                                                                       |
| *How do I inject DI services?*              | Resolve them from `$Services` or add them as parameters and decorate the script with `param($mySvc)` — Kestrun binds params from DI too. |
| *Can the script short-circuit the request?* | Absolutely. Return, or set `$Response.Status() / Redirect()` **and** `return`, and Razor won’t run.                                      |
| *Layout / partials?*                        | Works exactly as in normal ASP.NET Core Razor; place `_Layout.cshtml`, use `@{ Layout = "_Layout"; }`.                                   |
| *Why do I get “endpoint not found”?*        | Ensure `UseRouting()` and `MapRazorPages()` are in the pipeline **after** `UsePowerShellRazorPages()`.                                   |

---

## 9. Reference snippets

### Register the middleware manually

```csharp
app.UsePowerShellRazorPages(runspacePool,
    pagesRoot: Path.Combine(env.ContentRootPath, "Pages"),
    pattern: "**/*.cshtml.ps1");   // glob optional
```

### Build a runspace pool yourself

```csharp
var pool = new KestrunRunspacePoolManager(min: 2, max: 32);
app.UsePowerShellRazorPages(pool);
```

 