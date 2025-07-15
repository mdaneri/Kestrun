
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using System.Text;
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

        /// <summary>
        /// Global text encoding for all responses. Defaults to UTF-8.
        /// </summary>
        public static System.Text.Encoding TextEncoding { get; set; } = System.Text.Encoding.UTF8;
        /// <summary>
        /// If the response body is larger than this threshold (in bytes), async write will be used.
        /// </summary>
        public int BodyAsyncThreshold { get; set; } = 8192; // 8 KB default


        public KestrunResponse(int bodyAsyncThreshold = 8192)
        {
            BodyAsyncThreshold = bodyAsyncThreshold;
        }
        public string? GetHeader(string key)
        {
            return Headers.TryGetValue(key, out var value) ? value : null;
        }

        public static bool IsTextBasedContentType(string type)
        {
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
        /// <summary>
        /// Shortcut to send a file (as attachment or inline).
        /// </summary>
        /// <param name="filePath">Path to the file to send.</param>
        /// <param name="asAttachment">If true, the file will be sent as an attachment.</param>
        /// <param name="downloadName">Optional download name for the file.</param> 
        /// <param name="contentType">Optional content type. If not provided, it will be determined based on the file extension.</param>
        /// <param name="statusCode">HTTP status code for the response.</param> 
        /// <remarks>
        /// If the response body is larger than the specified threshold (in bytes), async write will be used.
        /// </remarks>
        public void WriteFileResponse(
            string filePath,
            bool asAttachment = true,
            string? downloadName = null,
            string? contentType = null,
            int statusCode = StatusCodes.Status200OK
        )
        {
            var fi = new FileInfo(filePath);
            var provider = new FileExtensionContentTypeProvider();
            if (contentType == null)
            {
                contentType = provider.TryGetContentType(filePath, out var ct)
                    ? ct
                    : "application/octet-stream";
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
            var dispType = asAttachment
                ? ContentDispositionType.Attachment
                : ContentDispositionType.Inline;
            ContentDisposition = new ContentDispositionOptions
            {
                FileName = downloadName ?? fi.Name,
                Type = dispType
            };
        }
        public void WriteJsonResponse(object? inputObject, int statusCode = StatusCodes.Status200OK)
        {
            WriteJsonResponse(inputObject, statusCode, depth: 10, compress: false);
        }

        public void WriteJsonResponse(object? inputObject, int statusCode, JsonSerializerSettings serializerSettings)
        {

            Body = JsonConvert.SerializeObject(inputObject, serializerSettings);
            ContentType = $"application/json; charset={Encoding.WebName}";
            StatusCode = statusCode;
        }


        public void WriteJsonResponse(object? inputObject, int statusCode, int depth, bool compress)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = compress ? Formatting.None : Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                MaxDepth = depth
            };
            WriteJsonResponse(inputObject, statusCode, settings);
        }

        public void WriteJsonResponse(object? inputObject, int statusCode, bool compress)
        {
            WriteJsonResponse(inputObject, statusCode, depth: 10, compress: compress);
        }

        public void WriteJsonResponse(object? inputObject, int statusCode, int depth)
        {
            WriteJsonResponse(inputObject, statusCode, depth: depth, compress: false);
        }

        public void WriteYamlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
        {
            Body = YamlHelper.ToYaml(inputObject);
            ContentType = contentType ?? $"application/yaml; charset={Encoding.WebName}";
            StatusCode = statusCode;
        }

        public void WriteXmlResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
        {
            XElement xml = XmlUtil.ToXml("Response", inputObject);

            Body = xml.ToString(SaveOptions.DisableFormatting);
            ContentType = contentType ?? $"application/xml; charset={Encoding.WebName}";
            StatusCode = statusCode;
        }

        public void WriteTextResponse(object? inputObject, int statusCode = StatusCodes.Status200OK, string? contentType = null)
        {
            Body = inputObject?.ToString() ?? string.Empty;
            ContentType = contentType ?? $"text/plain; charset={Encoding.WebName}";
            StatusCode = statusCode;
        }

        public void WriteRedirectResponse(string url, int statusCode = StatusCodes.Status302Found, string? message = null)
        {

            // framework hook
            RedirectUrl = url;

            // HTTP status + Location header
            StatusCode = statusCode;
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
            Body = data;
            ContentType = contentType;
            StatusCode = statusCode;
            Headers["Content-Length"] = data.Length.ToString();
        }

        public async Task ApplyTo(HttpResponse response)
        {
            if (!string.IsNullOrEmpty(RedirectUrl))
            {
                response.Redirect(RedirectUrl);
                return;
            }
            response.StatusCode = StatusCode;
            // Ensure charset is set for text content types
            string contentType = ContentType;
            if (contentType != null && contentType.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!contentType.Contains("charset=", System.StringComparison.OrdinalIgnoreCase))
                {
                    contentType = contentType.TrimEnd(';') + $"; charset={TextEncoding.WebName}";
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
                        if (bytes.Length > BodyAsyncThreshold)
                            await response.Body.WriteAsync(bytes);
                        else
                            response.Body.Write(bytes, 0, bytes.Length);
                        break;
                    case System.IO.Stream stream:
                        // Always use async for streams
                        await stream.CopyToAsync(response.Body);
                        break;
                    case string str:
                        var strBytes = TextEncoding.GetBytes(str);
                        if (strBytes.Length > BodyAsyncThreshold)
                            await response.Body.WriteAsync(strBytes);
                        else
                            response.Body.Write(strBytes, 0, strBytes.Length);
                        break;
                    default:
                        var fallback = Body.ToString() ?? string.Empty;
                        var fallbackBytes = TextEncoding.GetBytes(fallback);
                        if (fallbackBytes.Length > BodyAsyncThreshold)
                            await response.Body.WriteAsync(fallbackBytes);
                        else
                            response.Body.Write(fallbackBytes, 0, fallbackBytes.Length);
                        break;
                }
            }
        }
    }
}
