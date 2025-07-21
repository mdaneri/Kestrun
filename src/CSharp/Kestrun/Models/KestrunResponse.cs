
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using Org.BouncyCastle.Asn1.Ocsp;
using Serilog;
using Serilog.Events;
using System.Buffers;
using Microsoft.Extensions.FileProviders;
using System.Net.Mime;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using MongoDB.Bson;
namespace Kestrun;

public enum ContentDispositionType
{
    Attachment,
    Inline,
    NoContentDisposition
}
/// <summary>
/// Options for Content-Disposition header.
/// </summary>
public class ContentDispositionOptions
{
    public ContentDispositionOptions()
    {
        FileName = null;
        Type = ContentDispositionType.NoContentDisposition;
    }

    public string? FileName { get; set; }
    public ContentDispositionType Type { get; set; }

    public override string ToString()
    {
        if (Type == ContentDispositionType.NoContentDisposition)
            return string.Empty;

        var disposition = Type == ContentDispositionType.Attachment ? "attachment" : "inline";
        if (string.IsNullOrEmpty(FileName))
            return disposition;

        // Escape the filename to handle special characters
        var escapedFileName = WebUtility.UrlEncode(FileName);
        return $"{disposition}; filename=\"{escapedFileName}\"";
    }
}
public class KestrunResponse
{

    public int StatusCode { get; set; } = 200;
    public Dictionary<string, string> Headers { get; set; } = [];
    public string ContentType { get; set; } = "text/plain";
    public object? Body { get; set; }
    public string? RedirectUrl { get; set; } // For HTTP redirects
    public List<string>? Cookies { get; set; } // For Set-Cookie headers


    /// <summary>
    /// Text encoding for textual MIME types.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Content-Disposition header value.
    /// </summary>
    public ContentDispositionOptions ContentDisposition { get; set; }
    public KestrunRequest Request { get; private set; }

    /// <summary>
    /// Global text encoding for all responses. Defaults to UTF-8.
    /// </summary>
    //public static System.Text.Encoding TextEncoding { get; set; } = System.Text.Encoding.UTF8;

    public Encoding AcceptCharset { get; private set; }

    /// <summary>
    /// If the response body is larger than this threshold (in bytes), async write will be used.
    /// </summary>
    public int BodyAsyncThreshold { get; set; } = 8192; // 8 KB default

    #region Constructors
    public KestrunResponse(KestrunRequest request, int bodyAsyncThreshold = 8192)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        AcceptCharset = request.Headers.TryGetValue("Accept-Charset", out string? value) ? Encoding.GetEncoding(value) : Encoding.UTF8; // Default to UTF-8 if null
        BodyAsyncThreshold = bodyAsyncThreshold;
        ContentDisposition = new ContentDispositionOptions();
    }
    #endregion

    #region Helpers
    public string? GetHeader(string key)
    {
        return Headers.TryGetValue(key, out var value) ? value : null;
    }

    private string DetermineContentType(string? contentType, string defaultType = "text/plain")
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            Request.Headers.TryGetValue("Accept", out var acceptHeader);
            contentType = (acceptHeader ?? defaultType)
                                 .ToLowerInvariant();
        }

        return contentType;
    }

    public static bool IsTextBasedContentType(string type)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Checking if content type is text-based: {ContentType}", type);

        // Check if the content type is text-based or has a charset
        if (string.IsNullOrEmpty(type))
            return false;
        if (type.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        // Include structured types using XML or JSON suffixes
        if (type.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) ||
            type.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
            return true;

        // Common application types where charset makes sense
        return type.ToLowerInvariant() switch
        {
            "application/json" or "application/xml" or "application/javascript" or "application/xhtml+xml" or "application/x-www-form-urlencoded" or "application/yaml" or "application/graphql" => true,
            _ => false,
        };
    }
    #endregion

    #region  Response Writers
    public void WriteFileResponse(
        string? filePath,
        string? contentType,
        int statusCode = StatusCodes.Status200OK
    )
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing file response,FilePath={FilePath} StatusCode={StatusCode}, ContentType={ContentType}, CurrentDirectory={CurrentDirectory}",
                filePath, statusCode, contentType, Directory.GetCurrentDirectory());

        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        if (!File.Exists(filePath))
        {
            StatusCode = StatusCodes.Status404NotFound;
            Body = $"File not found: {filePath}";
            ContentType = $"text/plain; charset={Encoding.WebName}";
            return;
        }
        // 1. Make sure you have an absolute file path
        var fullPath = Path.GetFullPath(filePath);

        // 2. Extract the directory to use as the "root"
        var directory = Path.GetDirectoryName(fullPath)
                       ?? throw new InvalidOperationException("Could not determine directory from file path");

        //       var fi = new FileInfo(filePath);
        var physicalProvider = new PhysicalFileProvider(directory);
        IFileInfo fi = physicalProvider.GetFileInfo(Path.GetFileName(filePath));
        var provider = new FileExtensionContentTypeProvider();
        contentType ??= provider.TryGetContentType(fullPath, out var ct)
                ? ct
                : "application/octet-stream";
        Body = fi;

        // headers & metadata
        StatusCode = statusCode;
        ContentType = contentType;
        Log.Debug("File response prepared: FileName={FileName}, Length={Length}, ContentType={ContentType}",
            fi.Name, fi.Length, ContentType);

    }

    public void WriteJsonResponse(object? inputObject, int statusCode = StatusCodes.Status200OK)
    {
        WriteJsonResponse(inputObject, depth: 10, compress: false, statusCode: statusCode);
    }

    public void WriteJsonResponse(object? inputObject, JsonSerializerSettings serializerSettings, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing JSON response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

        Body = JsonConvert.SerializeObject(inputObject, serializerSettings);
        ContentType = string.IsNullOrEmpty(contentType) ? $"application/json; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Writes a CBOR response (binary, efficient, not human-readable).
    /// </summary>
    public void WriteCborResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing CBOR response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

        // Serialize to CBOR using PeterO.Cbor
        byte[] cborBytes = inputObject != null
            ? PeterO.Cbor.CBORObject.FromObject(inputObject).EncodeToBytes()
            : [];
        Body = cborBytes;
        ContentType = string.IsNullOrEmpty(contentType) ? "application/cbor" : contentType;
        StatusCode = statusCode;
    }



    public void WriteBsonResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing BSON response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

        // Serialize to BSON (as byte[])
        Body = inputObject != null ? inputObject.ToBson() : [];
        ContentType = string.IsNullOrEmpty(contentType) ? "application/bson" : contentType;
        StatusCode = statusCode;
    }

    public void WriteResponse(object? inputObject, int statusCode = StatusCodes.Status200OK)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing response, StatusCode={StatusCode}", statusCode);

        Body = inputObject;
        ContentType = DetermineContentType(string.Empty, "text/plain"); // Ensure ContentType is set based on Accept header

        if (ContentType.Contains("json"))
        {
            WriteJsonResponse(inputObject: inputObject, statusCode: statusCode);
        }
        else if (ContentType.Contains("yaml") || ContentType.Contains("yml"))
        {
            WriteYamlResponse(inputObject: inputObject, statusCode: statusCode);
        }
        else if (ContentType.Contains("xml"))
        {
            WriteXmlResponse(inputObject: inputObject, statusCode: statusCode);
        }
        else
        {
            WriteTextResponse(inputObject: inputObject, statusCode: statusCode);
        }
    }

    public void WriteJsonResponse(object? inputObject, int depth, bool compress, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing JSON response, StatusCode={StatusCode}, ContentType={ContentType}, Depth={Depth}, Compress={Compress}",
                statusCode, contentType, depth, compress);

        var serializerSettings = new JsonSerializerSettings
        {
            Formatting = compress ? Formatting.None : Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            MaxDepth = depth
        };
        WriteJsonResponse(inputObject, serializerSettings: serializerSettings, statusCode: statusCode, contentType: contentType);
    }

    public void WriteYamlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing YAML response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

        Body = YamlHelper.ToYaml(inputObject);
        ContentType = string.IsNullOrEmpty(contentType) ? $"application/yaml; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }

    public void WriteXmlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing XML response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

        XElement xml = XmlUtil.ToXml("Response", inputObject);

        Body = xml.ToString(SaveOptions.DisableFormatting);
        ContentType = string.IsNullOrEmpty(contentType) ? $"application/xml; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }

    public void WriteTextResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
    {

        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing text response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

        if (inputObject is null)
            throw new ArgumentNullException(nameof(inputObject), "Input object cannot be null for text response.");

        Body = inputObject?.ToString() ?? string.Empty;
        ContentType = string.IsNullOrEmpty(contentType) ? $"text/plain; charset={Encoding.WebName}" : contentType;
        StatusCode = statusCode;
    }

    public void WriteRedirectResponse(string url, string? message = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing redirect response, StatusCode={StatusCode}, Location={Location}", StatusCode, url);

        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url), "URL cannot be null for redirect response.");
        // framework hook
        RedirectUrl = url;

        // HTTP status + Location header
        StatusCode = StatusCodes.Status302Found;
        Headers["Location"] = url;

        if (message is not null)
        {
            // include a body
            Body = message;
            ContentType = $"text/plain; charset={Encoding.WebName}";
        }
        else
        {
            // no body: clear any existing body/headers
            Body = null;
            //ContentType = null;
            Headers.Remove("Content-Length");
        }

    }
    public void WriteBinaryResponse(byte[] data, int statusCode = StatusCodes.Status200OK, string contentType = "application/octet-stream")
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing binary response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        Body = data ?? throw new ArgumentNullException(nameof(data), "Data cannot be null for binary response.");
        ContentType = contentType;
        StatusCode = statusCode;
        //       Headers["Content-Length"] = data.Length.ToString();
    }

    public void WriteStreamResponse(Stream stream, int statusCode = StatusCodes.Status200OK, string contentType = "application/octet-stream")
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing stream response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
        Body = stream;
        ContentType = contentType;
        StatusCode = statusCode;
        //      Headers["Content-Length"] = stream.Length.ToString();
    }
    #endregion

    #region Error Responses
    /// <summary>
    /// Structured payload for error responses.
    /// </summary>
    internal record ErrorPayload
    {
        public string Error { get; init; } = default!;
        public string? Details { get; init; }
        public string? Exception { get; init; }
        public string? StackTrace { get; init; }
        public int Status { get; init; }
        public string Reason { get; init; } = default!;
        public string Timestamp { get; init; } = default!;
        public string? Path { get; init; }
        public string? Method { get; init; }
    }

    /// <summary>
    /// Write an error response with a custom message.
    /// Chooses JSON/YAML/XML/plain-text based on override → Accept → default JSON.
    /// </summary>
    public void WriteErrorResponse(
        string message,
        int statusCode = StatusCodes.Status500InternalServerError,
        string? contentType = null,
        string? details = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing error response, StatusCode={StatusCode}, ContentType={ContentType}, Message={Message}",
                statusCode, contentType, message);
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentNullException(nameof(message));

        Log.Warning("Writing error response with status {StatusCode}: {Message}", statusCode, message);

        var payload = new ErrorPayload
        {
            Error = message,
            Details = details,
            Exception = null,
            StackTrace = null,
            Status = statusCode,
            Reason = ReasonPhrases.GetReasonPhrase(statusCode),
            Timestamp = DateTime.UtcNow.ToString("o"),
            Path = Request?.Path,
            Method = Request?.Method
        };

        WriteFormattedErrorResponse(payload, contentType);
    }

    /// <summary>
    /// Write an error response based on an exception.
    /// Chooses JSON/YAML/XML/plain-text based on override → Accept → default JSON.
    /// </summary>
    public void WriteErrorResponse(
        Exception ex,
        int statusCode = StatusCodes.Status500InternalServerError,
        string? contentType = null,
        bool includeStack = true)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing error response from exception, StatusCode={StatusCode}, ContentType={ContentType}, IncludeStack={IncludeStack}",
                statusCode, contentType, includeStack);

        ArgumentNullException.ThrowIfNull(ex);

        Log.Warning(ex, "Writing error response with status {StatusCode}", statusCode);

        var payload = new ErrorPayload
        {
            Error = ex.Message,
            Details = null,
            Exception = ex.GetType().Name,
            StackTrace = includeStack ? ex.ToString() : null,
            Status = statusCode,
            Reason = ReasonPhrases.GetReasonPhrase(statusCode),
            Timestamp = DateTime.UtcNow.ToString("o"),
            Path = Request?.Path,
            Method = Request?.Method
        };

        WriteFormattedErrorResponse(payload, contentType);
    }


    /// <summary>
    /// Internal dispatcher: serializes the payload according to the chosen content-type.
    /// </summary>
    private void WriteFormattedErrorResponse(ErrorPayload payload, string? contentType = null)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Writing formatted error response, ContentType={ContentType}, Status={Status}", contentType, payload.Status);
        if (string.IsNullOrWhiteSpace(contentType))
        {
            Request.Headers.TryGetValue("Accept", out var acceptHeader);
            contentType = (acceptHeader ?? "text/plain")
                                 .ToLowerInvariant();
        }
        if (contentType.Contains("json"))
        {
            WriteJsonResponse(payload, payload.Status);
        }
        else if (contentType.Contains("yaml") || contentType.Contains("yml"))
        {
            WriteYamlResponse(payload, payload.Status);
        }
        else if (contentType.Contains("xml"))
        {
            WriteXmlResponse(payload, payload.Status);
        }
        else
        {
            // Plain-text fallback
            var lines = new List<string>
                {
                    $"Status: {payload.Status} ({payload.Reason})",
                    $"Error: {payload.Error}",
                    $"Time: {payload.Timestamp}"
                };

            if (!string.IsNullOrWhiteSpace(payload.Details))
                lines.Add("Details:\n" + payload.Details);

            if (!string.IsNullOrWhiteSpace(payload.Exception))
                lines.Add($"Exception: {payload.Exception}");

            if (!string.IsNullOrWhiteSpace(payload.StackTrace))
                lines.Add("StackTrace:\n" + payload.StackTrace);

            var text = string.Join("\n", lines);
            WriteTextResponse(text, payload.Status, "text/plain");
        }
    }
    #endregion

    #region Apply to HttpResponse
    public async Task ApplyTo(HttpResponse response)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
            Log.Debug("Applying KestrunResponse to HttpResponse, StatusCode={StatusCode}, ContentType={ContentType}, BodyType={BodyType}",
                StatusCode, ContentType, Body?.GetType().Name ?? "null");

        if (!string.IsNullOrEmpty(RedirectUrl))
        {
            response.Redirect(RedirectUrl);
            return;
        }

        try
        {
            response.StatusCode = StatusCode;
            // Ensure charset is set for text content types

            if (!string.IsNullOrEmpty(ContentType) && IsTextBasedContentType(ContentType) && (!ContentType.Contains("charset=", System.StringComparison.OrdinalIgnoreCase)))
            {
                ContentType = ContentType.TrimEnd(';') + $"; charset={AcceptCharset.WebName}";
            }
            response.ContentType = ContentType;
            if (ContentDisposition.Type != ContentDispositionType.NoContentDisposition)
            {
                // Set Content-Disposition header based on type
                // Use the ContentDispositionType enum to determine the disposition value
                if (Log.IsEnabled(LogEventLevel.Debug))
                    Log.Debug("Setting Content-Disposition header, Type={Type}, FileName={FileName}",
                              ContentDisposition.Type, ContentDisposition.FileName);
                string dispositionValue = ContentDisposition.Type switch
                {
                    ContentDispositionType.Attachment => "attachment",
                    ContentDispositionType.Inline => "inline",
                    _ => throw new InvalidOperationException("Invalid Content-Disposition type")
                };

                // If no filename is provided, use the default filename based on the body type
                if (string.IsNullOrEmpty(ContentDisposition.FileName) && Body is not null && Body is IFileInfo fileInfo)
                {
                    ContentDisposition.FileName = fileInfo.Name;// If no filename is provided, use the file's name
                }

                // If a filename is provided, append it to the disposition value
                if (!string.IsNullOrEmpty(ContentDisposition.FileName))
                {
                    // Escape the filename to handle special characters
                    var escapedFileName = WebUtility.UrlEncode(ContentDisposition.FileName);
                    dispositionValue += $"; filename=\"{escapedFileName}\"";
                }
                response.Headers.Append("Content-Disposition", dispositionValue);

            }

            if (Headers != null)
            {
                foreach (var kv in Headers)
                {
                    response.Headers[kv.Key] = kv.Value;
                }
            }
            if (Cookies != null)
            {
                foreach (var cookie in Cookies)
                {
                    response.Headers.Append("Set-Cookie", cookie);
                }
            }
            if (Body != null)
            {
                switch (Body)
                {
                    case IFileInfo fileInfo:
                        Log.Debug("Sending file {FileName} (Length={Length})", fileInfo.Name, fileInfo.Length);
                        response.ContentLength = fileInfo.Length;   // conveys intent & avoids string conversion 
                        response.Headers.LastModified = fileInfo.LastModified.ToString("R");
                        await response.SendFileAsync(
                            file: fileInfo,
                            offset: 0,
                            count: fileInfo.Length,
                            cancellationToken: response.HttpContext.RequestAborted
                        );
                        break;
                    case byte[] bytes:
                        response.ContentLength = bytes.LongLength;
                        await response.Body.WriteAsync(bytes, response.HttpContext.RequestAborted);
                        await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                        break;
                    case System.IO.Stream stream:
                        bool seekable = stream.CanSeek;
                        Log.Debug("Sending stream (seekable={Seekable}, len={Len})",
                                  seekable, seekable ? stream.Length : -1);

                        // If you *do* know length, set header; otherwise strip it
                        if (seekable)
                        {
                            // ensure caller did not already set Content-Length
                            response.ContentLength = stream.Length;
                            stream.Position = 0;
                        }
                        else
                        {
                            response.ContentLength = null; // no length for non-seekable streams
                        }

                        // copy async in 32 kB chunks (BodyAsyncThreshold is your buffer size)
                        //    await stream.CopyToAsync(response.Body, BodyAsyncThreshold,
                        //                            response.HttpContext.RequestAborted);

                        const int BufferSize = 64 * 1024; // 64 KB
                        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                        try
                        {
                            int bytesRead;
                            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, BufferSize), response.HttpContext.RequestAborted)) > 0)
                            {
                                // using the new Memory-based overload avoids an extra copy
                                await response.Body.WriteAsync(buffer.AsMemory(0, bytesRead),
                                                               response.HttpContext.RequestAborted);
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }
                        // Ensure the response is flushed after writing the stream
                        // This is important for non-seekable streams to ensure all data is sent
                        await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                        break;

                    case string str:
                        // Encode once
                        var data = AcceptCharset.GetBytes(str);

                        // Optionally set length (remove it if you prefer chunked for text)
                        response.ContentLength = data.Length;

                        await response.Body.WriteAsync(data, response.HttpContext.RequestAborted);
                        await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                        break;

                    default:
                        Body = "Unsupported body type: " + Body.GetType().Name;
                        Log.Warning("Unsupported body type: {BodyType}", Body.GetType().Name);
                        response.StatusCode = StatusCodes.Status500InternalServerError;
                        response.ContentType = "text/plain; charset=utf-8";
                        response.ContentLength = Body.ToString()?.Length ?? null;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying response: {ex.Message}");
            // Optionally, you can log the exception or handle it as needed
            throw;
        }
    }
    #endregion
}
