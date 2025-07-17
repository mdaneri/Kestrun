namespace KestrumLib
{
    /// <summary>
    /// Simplified representation of an incoming HTTP request used by script handlers.
    /// </summary>
    public class KestrunRequest
    {
        public required string Method { get; set; }
        public required string Path { get; set; }
        public required Dictionary<string, string> Query { get; set; }
        public required Dictionary<string, string> Headers { get; set; }
        public required string Body { get; set; }

        /// <summary>
        /// Creates a <see cref="KestrunRequest"/> from the provided <see cref="HttpContext"/>.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A populated <see cref="KestrunRequest"/> instance.</returns>
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