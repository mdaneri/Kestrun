---
title: Logging
parent: Tutorials
nav_order: 2
---

# Kestrun Logging

## Overview

Kestrun’s logging system builds on Serilog to give you:

* **Multiple named loggers** — each with its own minimum level, enrichers, and sinks.
* **Full Serilog flexibility** — use any sink (console, file, Seq, HTTP, syslog…), any enricher, any output template.
* **Global fallback** — the library itself and any PowerShell scripts that don’t specify a logger still write to `Serilog.Log`.
* **Global fallback** – when no named logger is specified, log events go through `Serilog.Log`.
* **PowerShell support** — configure and invoke named loggers directly from PS scripts.

Under the hood we provide:

| C# Type / PS Module                          | Purpose                                                                                                                                                |
|----------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| `KestrunLogConfigurator`                     | Static registry; creates, stores and re-configures loggers.                                                                                            |
| `KestrunLoggerBuilder`                       | Fluent builder that wraps **Serilog.LoggerConfiguration** & adds helpers (`Minimum`, `WithProperty`, `With<TEnricher>`, `Sink(...)`, `Register(...)`). |
| **PowerShell**   | Cmdlets that surface the builder pattern in PS pipelines (`New-KrLogger`, `Add-KrProperty`, `Add-KrSink`, …).                                          |

---

### 1. Initializing a Logger

```csharp
// 1. Start a builder for a named logger “api”.
//    If not seen before, creates new LoggerConfiguration with FromLogContext().
var builder = KestrunLogConfigurator.Configure("api");
```

### 2. Setting the Minimum Level

```csharp
builder.Minimum(LogEventLevel.Debug);
// Events at Debug or above will be emitted; Verbose events are dropped.
```

### 3. Adding Enrichment

```csharp
// A. Static properties:
builder.WithProperty("Subsystem", "API");

// B. Built-in enrichers (parameter-less):
builder.With<Serilog.Enrichers.Thread.ThreadIdEnricher>();

// C. Custom enrichers with ctor args:
builder.With<MyCustomEnricher>("someSetting");
```

Under the hood `WithProperty` and `With<TEnricher>` invoke the same Serilog enrichment pipeline your normal Serilog code uses.

### 4. Adding Sinks

You can add as many sinks as you like. Each event will be duplicated to every sink.

```csharp
// Console sink with custom template
builder.Sink(w => w.Console(
    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message}{NewLine}{Exception}"));

// Rolling file sink
builder.Sink(w => w.File(
    path: "logs/api-.log",
    rollingInterval: RollingInterval.Day));

// HTTP sink (example)
builder.Sink(w => w.DurableHttpUsingFileSizeRolledBuffers(
    requestUri: "https://logs.mycompany.com/api/logs"));
```

### 5. Finalizing the Logger

```csharp
// Build and register under “api”. Does NOT replace Serilog.Log by default.
var apiLog = builder.Register();

// If you want this logger to become the new global default:
builder.Register(setAsDefault: true);
```

Once applied, you can immediately call:

```csharp
apiLog.Information("User {UserId} requested {Path}", userId, req.Path);
```

---

## Complete C# Examples

### Example A: Basic “api” Logger

```csharp
KestrunLogConfigurator.Configure("api")
    .Minimum(LogEventLevel.Information)
    .WithProperty("Subsystem", "API")
    .Sink(w => w.Console())
    .Register();  // Does not change Serilog.Log.Logger
```

### Example B: Two Loggers, Different Levels & Sinks

```csharp
// 1️⃣  Audit log: only warnings and above, writes JSON files
KestrunLogConfigurator.Configure("audit")
    .Minimum(LogEventLevel.Warning)
    .Sink(w => w.File(
        path: "logs/audit-.json",
        rollingInterval: RollingInterval.Day,
        formatter: new Serilog.Formatting.Json.JsonFormatter()))
    .Register();

// 2️⃣  Debug log: verbose output to console + Seq
KestrunLogConfigurator.Configure("debug")
    .Minimum(LogEventLevel.Verbose)
    .Sink(w => w.Console(outputTemplate: "[{Level:u3}] {Message}"))
    .Sink(w => w.Seq(serverUrl: "http://localhost:5341"))
    .Register();

// 3️⃣  Use them:
var audit = KestrunLogConfigurator.Get("audit")!;
audit.Error("Failed to process order {OrderId}", orderId);

var debug = KestrunLogConfigurator.Get("debug")!;
debug.Verbose("Processing {StepName}", stepName);
```

### Example C: Promote One as Global Default

```csharp
// Replace the built-in Serilog.Log.Logger with this custom logger
KestrunLogConfigurator.Configure("default")
    .Minimum(LogEventLevel.Debug)
    .WithProperty("App", "KestrunService")
    .Sink(w => w.Console())
    .Register(setAsDefault: true);

// Anywhere else, Serilog.Log.Information(...) now goes through “default”
Log.Information("Service started at {Start}", DateTime.UtcNow);
```

---

## PowerShell Usage

```powershell

# 1. Create a named logger “ps”
New-KrLogger -Name "ps" -Level Debug |
    Add-KrSink -Type File -Options @{ Path = "logs/ps-.log" } |
    Add-KrSink -Type Console -Options @{} |
    Register-KrLogger

# 2. Write to it anywhere in your PS routes
Write-KrLog -Name "ps" -Level Information `
    -Message "Handled {Method} {Path}" -Args $Context.Request.Method, $Context.Request.Path

Write-KrLog -Name "ps" -Level Information `
            -Message "Handled {Method} {Path}" `
            -Arguments $Context.Request.Method, $Context.Request.Path `
            -Properties @{ CorrelationId = $cid }
```

If you pass `-Default` to `Apply-KrLogger`, it swaps in your PS logger as `Serilog.Log.Logger` so any C# or framework logs go to your sinks too.

---

## Resetting & Hot-Reload

When you need to tear down and rebuild all loggers (e.g. upon config-file change or between tests), call:

csharp

```csharp
KestrunLogConfigurator.Reset();
```

powershell

```powershell
ReSet-KrDefaultLogger -Name 'ps'
```

This will:

1. **Flush** all pending events.
2. **Dispose** each custom logger.
3. **Clear** internal registries.
4. **Restore** a minimal console logger as `Serilog.Log.Logger`.

After `Reset()` you can re-run your `Configure(…)…Register()` calls or (in PS) your
`New-KrLogger … | Register-KrLogger` pipelines to spin loggers back up.

---

## 3. PowerShell cmdlet reference

| Cmdlet                                                                                                                                                                                                                | What it does                                                                                                                                   | Typical pipeline position |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------|
| **`New-KrLogger`**<br/>`-Name` *string*<br/>`[-Level]` *Verbose∣Debug∣…*                                                                                                                                              | Starts a new *builder* for the named logger (or returns existing builder to mutate).                                                           | 1st                       |
| **`Add-KrProperty`**<br/>`-Builder` *Builder*<br/>`-Name` *string* `-Value` *object*                                                                                                                                  | Adds a static property.                                                                                                                        | anywhere                  |
| **`Add-KrEnricher`**<br/>`-TypeName` *"Namespace.Type"* `[-Arguments]` *object\[]*                                                                                                                                    | Adds an `ILogEventEnricher`.                                                                                                                   | anywhere                  |
| **`Add-KrSink`**<br/>`-Type` *Console∣File∣Seq∣Http∣Syslog∣Custom*<br/>`[-Options]` *hashtable*<br/>`[-CustomSink]` *ILogEventSink*                                                                                   | Attaches a sink.                                                                                                                               | anywhere                  |
| **`Register-KrLogger`** `[-Default]`                                                                                                                                                                                  | Builds the `ILogger`, stores it, optionally makes it global.                                                                                   | last                      |
| **`Write-KrLog`**<br/>`[-Name]` *string*<br/>`-Level` *Verbose∣…*<br/>`-Message` *string*<br/>`[-Arguments]` *object\[]*<br/>`[-Exception]` *\[Exception]*<br/>`[-Properties]` *hashtable*<br/>`[-Object]` *psobject* | Emits an event through a named logger or `Serilog.Log`. Supports extra properties **and** arbitrary PS objects (`-Object` serialises to JSON). | n/a                       |
| **`Update-KrLogger`**<br/>`-Name` *string*<br/>`-ScriptBlock` *{ param(\$cfg) … }*<br/>`[-Default]`                                                                                                                   | Re-builds the logger by replaying its recipe **plus** your script-block delta.                                                                 | n/a                       |
| **`Get-KrLogger`** `-Name`                                                                                                                                                                                            | Returns the live `ILogger` (or throws).                                                                                                        | n/a                       |
| **`Test-KrLogger`** `-Name`                                                                                                                                                                                           | `True/False` – does the logger exist?                                                                                                          | n/a                       |
| **`ReSet-KrDefaultLogger`** `-Name`                                                                                                                                                                                           | Reset the Logger (or THrow)                                                                                                          | n/a                       |

---

## 4. Complete C# examples

### A.  Basic “api” logger

```csharp
KestrunLogConfigurator.Configure("api")
    .Minimum(LogEventLevel.Information)
    .WithProperty("Subsystem", "API")
    .Sink(w => w.Console())
    .Register();
```

### B.  Two loggers, different levels & sinks

```csharp
// Audit – JSON files, warnings+
KestrunLogConfigurator.Configure("audit")
    .Minimum(LogEventLevel.Warning)
    .Sink(w => w.File("logs/audit-.json",
                      rollingInterval: RollingInterval.Day,
                      formatter: new JsonFormatter()))
    .Register();

// Debug – verbose console + Seq
KestrunLogConfigurator.Configure("debug")
    .Minimum(LogEventLevel.Verbose)
    .Sink(w => w.Console("[{Level:u3}] {Message}"))
    .Sink(w => w.Seq("http://localhost:5341"))
    .Register();
```

### C.  Promote one as global default

```csharp
KestrunLogConfigurator.Configure("default")
    .Minimum(LogEventLevel.Debug)
    .Sink(w => w.Console())
    .Register(setAsDefault: true);
```

## Available Enrichers

> **How to use**
>
> ```csharp
> .WithProperty("Key", value)         // custom
> .With<YourEnricher>()               // parameter-less constructor
> .With<YourEnricher>(arg1, arg2)     // ctor args
> ```

| Enricher                        | Description                                                                                                                  | Example                                                           |
|---------------------------------|------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| **`WithProperty(name, value)`** | Injects a static property on every event.                                                                                    | `.WithProperty("Module", "API")`                                  |
| **`WithThreadId()`**            | Adds the current thread’s managed ID (`ThreadId` property).                                                                  | `.With<Serilog.Enrichers.Thread.ThreadIdEnricher>()`              |
| **`WithMachineName()`**         | Adds the host machine’s name (`MachineName` property).                                                                       | `.With<Serilog.Enrichers.Environment.MachineNameEnricher>()`      |
| **`WithProcessId()`**           | Adds the current process ID (`ProcessId` property).                                                                          | `.With<Serilog.Enrichers.Process.ProcessIdEnricher>()`            |
| **`WithExceptionDetails()`**    | Captures full exception details (stack, inner exceptions) for richer error logs. Requires the \[Serilog.Exceptions] package. | `.With<Serilog.Exceptions.Destructuring.DestructuringEnricher>()` |
| **`FromLogContext()`**          | (Applied by default) allows ambient context (via `LogContext.PushProperty`) to flow into events.                             | implicit on every builder                                         |
| **Custom `ILogEventEnricher`**  | Any custom enricher you write — implement `ILogEventEnricher` and attach with `With<T>()`.                                   | `.With<YourApp.UserNameEnricher>("claimType")`                    |

> **Tip:** You can mix and match any enrichers in a single chain:
>
> ```csharp
> .WithProperty("Subsystem", "Auth")
> .WithThreadId()
> .WithMachineName()
> .With<CustomRegionEnricher>(regionConfig)
> ```

---

## Common Sinks

> **How to use**
>
> ```csharp
> .Sink(w => w.Console(...))
> .Sink(w => w.File(...))
> .Sink(w => w.Seq(...))
> .Sink(w => w.DurableHttpUsingFileSizeRolledBuffers(...))
> .Sink(w => w.UdpSyslog(...))
> // …or any Serilog sink extension you reference
> ```

| Sink                                 | Package / Namespace                            | Description                                                                          | Example                                                                                                                                |
|--------------------------------------|------------------------------------------------|--------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------|
| **Console**                          | `Serilog.Sinks.Console`                        | Writes events to standard output or error streams.                                   | `.Sink(w => w.Console(outputTemplate: "[{Level:u3}] {Message}{NewLine}{Exception}"))`                                                  |
| **File**                             | `Serilog.Sinks.File`                           | Appends events to a file (supports rolling by time/size).                            | `.Sink(w => w.File("logs/app-.log", rollingInterval: RollingInterval.Day))`                                                            |
| **Seq**                              | `Serilog.Sinks.Seq`                            | Sends events to a Seq server over HTTP for structured log storage and querying.      | `.Sink(w => w.Seq("http://localhost:5341"))`                                                                                           |
| **Durable HTTP**                     | `Serilog.Sinks.Http`                           | Buffers events to disk and posts in batches to an HTTP endpoint.                     | `.Sink(w => w.DurableHttpUsingFileSizeRolledBuffers("https://logs.mycompany.com/api/logs"))`                                           |
| **UDP Syslog**                       | `Serilog.Sinks.SyslogMessages`                 | Sends events over UDP to a syslog server (RFC 5424).                                 | `.Sink(w => w.UdpSyslog("syslog.company.com", 514))`                                                                                   |
| **Rolling File with JSON Formatter** |                                                | Same as File sink but formats each event as JSON                                     | `.Sink(w => w.File("logs/audit-.json", rollingInterval: RollingInterval.Day, formatter: new Serilog.Formatting.Json.JsonFormatter()))` |
| **Custom Sink**                      | any `ILogEventSink` you implement or reference | Drop events anywhere you like—e.g. Azure Tables, Elasticsearch, AWS CloudWatch, etc. | `.Sink(w => w.Sink(new MyCustomSink()))`                                                                                               |

> **Note:** Because `Sink(...)` takes a `Func<LoggerSinkConfiguration, LoggerConfiguration>`, you can invoke *any* extension method provided by the sinks you reference—even ones not listed here.

 