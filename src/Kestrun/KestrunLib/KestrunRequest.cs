namespace KestrumLib
{
    public class KestrunRequest
    {
        public required string Method { get; set; }
        public required string Path { get; set; }
        public required Dictionary<string, string> Query { get; set; }
        public required Dictionary<string, string> Headers { get; set; }
        public required string Body { get; set; }

        static public async Task<KestrunRequest> NewRequest(HttpContext context)
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            // context.Request.Headers.Remove("Accept-Encoding");
            return new KestrunRequest
            {
                Method = context.Request.Method,
                Path = context.Request.Path.ToString(),
                Query = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString()),
                Headers = context.Request.Headers.ToDictionary(x => x.Key, x => x.Value.ToString()),
                Body = body
            };
        }
    }
}