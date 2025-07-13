
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KestrelLib
{
    class KestrunResponse
    {
        public int StatusCode { get; set; } = 200;
        public Dictionary<string, string> Headers { get; set; } = [];
        public string ContentType { get; set; } = "text/plain";
        public object? Body { get; set; }
        public string? RedirectUrl { get; set; } // For HTTP redirects
        public List<string>? Cookies { get; set; } // For Set-Cookie headers
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

        public void WriteJsonResponse(object inputObject, int statusCode = 200, JsonSerializerSettings? settings = null)
        {
            settings ??= new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                MaxDepth = 10
            };
            Body = JsonConvert.SerializeObject(inputObject, settings);
            ContentType = "application/json; charset=utf-8";
            StatusCode = statusCode;
        }

        public void WriteJsonResponse(object inputObject, int depth, int statusCode = 200, bool compress = false)
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
            Body = JsonConvert.SerializeObject(inputObject, settings);
            ContentType = "application/json; charset=utf-8";
            StatusCode = statusCode;
        }



        public void WriteYamlResponse(object inputObject, int depth, int statusCode = 200)
        { 
            Body = YamlHelper.ToYaml(inputObject);
            ContentType = "application/yaml; charset=utf-8";
            StatusCode = statusCode;
        }


        
        public void WriteTextResponse(object inputObject, int statusCode = 200)
        { 
            Body = inputObject.ToString() ?? string.Empty;
            ContentType = "text/plain; charset=utf-8";
            StatusCode = statusCode;
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
