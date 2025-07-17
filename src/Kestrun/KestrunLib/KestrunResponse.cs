
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
using Org.BouncyCastle.Asn1.Ocsp;
using Serilog;
using Serilog.Events;
namespace KestrumLib
{


    public class KestrunResponse
    {
        public enum ContentDispositionType
        {
            Attachment,
            Inline,
            NoContentDisposition
        }

        /// <summary>
        /// Options for Content-Disposition header.
        /// </summary>
        public record ContentDispositionOptions(
               string? FileName = null,
               ContentDispositionType Type = ContentDispositionType.NoContentDisposition
           );

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
        public ContentDispositionOptions ContentDisposition { get; set; } = new ContentDispositionOptions();
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


        public KestrunResponse(KestrunRequest request, int bodyAsyncThreshold = 8192)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            AcceptCharset = request.Headers.TryGetValue("Accept-Charset", out string? value) ? Encoding.GetEncoding(value) : Encoding.UTF8; // Default to UTF-8 if null
            BodyAsyncThreshold = bodyAsyncThreshold;
        }
        public string? GetHeader(string key)
        {
            return Headers.TryGetValue(key, out var value) ? value : null;
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
            switch (type.ToLowerInvariant())
            {
                case "application/json":
                case "application/xml":
                case "application/javascript":
                case "application/xhtml+xml":
                case "application/x-www-form-urlencoded":
                case "application/yaml":
                case "application/graphql":
                    return true;
            }

            return false;
        }

        public void WriteFileResponse(
            string? filePath,
            bool inline,
            string? fileDownloadName,
            string? contentType,
            bool embedFileContent,
            int statusCode = StatusCodes.Status200OK
        )
        {

            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Writing file response,FilePath={FilePath} StatusCode={StatusCode}, ContentType={ContentType},EmbedFileContent={EmbedFileContent}, CurrentDirectory={CurrentDirectory}",
                    filePath, statusCode, contentType, embedFileContent, Directory.GetCurrentDirectory());

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (!File.Exists(filePath))
            {
                StatusCode = StatusCodes.Status404NotFound;
                Body = $"File not found: {filePath}";
                ContentType = $"text/plain; charset={Encoding.WebName}";
                return;
            }

            var fi = new FileInfo(filePath);
            var provider = new FileExtensionContentTypeProvider();
            contentType ??= provider.TryGetContentType(filePath, out var ct)
                    ? ct
                    : "application/octet-stream";
            if (embedFileContent)
            {
                // load entire file into Body
                if (IsTextBasedContentType(contentType))
                    Body = File.ReadAllText(filePath, Encoding);
                else
                    Body = File.ReadAllBytes(filePath);
            }
            else
            {
                // body as stream
                Body = File.OpenRead(filePath);
            }


            if (IsTextBasedContentType(contentType) &&
                !contentType.Contains("charset=", StringComparison.OrdinalIgnoreCase))
            {
                contentType += $"; charset={Encoding.WebName}";
            }

            // headers & metadata
            StatusCode = statusCode;
            ContentType = contentType;
            Headers["Content-Length"] = fi.Length.ToString();

            // content‑disposition
            var dispType = inline
                ? ContentDispositionType.Inline
                : ContentDispositionType.Attachment;
            ContentDisposition = new ContentDispositionOptions
            {
                FileName = fileDownloadName ?? fi.Name,
                Type = dispType
            };
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
            ContentType = contentType ?? $"application/json; charset={Encoding.WebName}";
            StatusCode = statusCode;
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
            ContentType = contentType ?? $"application/yaml; charset={Encoding.WebName}";
            StatusCode = statusCode;
        }

        public void WriteXmlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Writing XML response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

            XElement xml = XmlUtil.ToXml("Response", inputObject);

            Body = xml.ToString(SaveOptions.DisableFormatting);
            ContentType = contentType ?? $"application/xml; charset={Encoding.WebName}";
            StatusCode = statusCode;
        }

        public void WriteTextResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
        {

            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Writing text response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);

            if (inputObject is null)
                throw new ArgumentNullException(nameof(inputObject), "Input object cannot be null for text response.");

            Body = inputObject?.ToString() ?? string.Empty;
            ContentType = contentType ?? $"text/plain; charset={Encoding.WebName}";
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

                // compute byte‑length of the message in the chosen encoding
                var bytes = Encoding.GetBytes(message);
                Headers["Content-Length"] = bytes.Length.ToString();
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
            Headers["Content-Length"] = data.Length.ToString();
        }

        public void WriteStreamResponse(Stream stream, int statusCode = StatusCodes.Status200OK, string contentType = "application/octet-stream")
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
                Log.Debug("Writing stream response, StatusCode={StatusCode}, ContentType={ContentType}", statusCode, contentType);
            Body = stream;
            ContentType = contentType;
            StatusCode = statusCode;
            Headers["Content-Length"] = stream.Length.ToString();
        }

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
                string contentType = ContentType;
                if (contentType != null && contentType.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!contentType.Contains("charset=", System.StringComparison.OrdinalIgnoreCase))
                    {
                        contentType = contentType.TrimEnd(';') + $"; charset={AcceptCharset.WebName}";
                    }
                }
                response.ContentType = contentType;
                if (ContentDisposition.Type != ContentDispositionType.NoContentDisposition)
                {
                    string dispositionValue = ContentDisposition.Type switch
                    {
                        ContentDispositionType.Attachment => "attachment",
                        ContentDispositionType.Inline => "inline",
                        _ => throw new InvalidOperationException("Invalid Content-Disposition type")
                    };

                    if (!string.IsNullOrEmpty(ContentDisposition.FileName))
                    {
                        dispositionValue += $"; filename=\"{ContentDisposition.FileName}\"";
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
                        case byte[] bytes:

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
                                response.Headers["Content-Length"] = stream.Length.ToString();
                                stream.Position = 0;
                            }
                            else
                            {
                                response.Headers.Remove("Content-Length");
                            }

                            // copy async in 32 kB chunks (BodyAsyncThreshold is your buffer size)
                            await stream.CopyToAsync(response.Body, BodyAsyncThreshold,
                                                      response.HttpContext.RequestAborted);

                            await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                            break;
                        /*          Log.Debug("Writing stream response, Length={Length}, CanSeek={CanSeek}", stream.Length, stream.CanSeek);
                                  if (stream.CanSeek && stream.Length > BodyAsyncThreshold)
                                  {
                                     // response.Headers.Remove("Content-Length"); // Content-Length is not reliable for streams
                                      stream.Position = 0; // Reset position to start
                                      // If the stream is seekable and large, use async write
                                      await stream.CopyToAsync(response.Body);
                                      // Reset position if the stream is seekable
                                      stream.Position = 0;
                                      Log.Debug("Stream position reset to 0 after copying to response body.");
                                  }
                                  else
                                  {
                                      // If the stream is seekable and small, write synchronously
                                      stream.Position = 0; // Reset position to start
                                      stream.CopyTo(response.Body);
                                      Log.Debug("Stream position reset to 0 after copying to response body.");
                                  }
      */
                        case string str:
                            // Encode once
                            var data = AcceptCharset.GetBytes(str);

                            // Optionally set length (remove it if you prefer chunked for text)
                            response.Headers["Content-Length"] = data.Length.ToString();

                            await response.Body.WriteAsync(data, response.HttpContext.RequestAborted);
                            await response.Body.FlushAsync(response.HttpContext.RequestAborted);
                            break;
                        default:
                            var fallback = Body.ToString() ?? string.Empty;
                            var fallbackBytes = AcceptCharset.GetBytes(fallback);
                            if (fallbackBytes.Length > BodyAsyncThreshold)
                                await response.Body.WriteAsync(fallbackBytes);
                            else
                                response.Body.Write(fallbackBytes, 0, fallbackBytes.Length);
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
    }
}
