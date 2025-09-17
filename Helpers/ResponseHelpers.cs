using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API.Helpers
{
    public class ResponseHelpers
    {
        public static IActionResult CachedJson<T>(HttpRequest req, T data, JsonSerializerOptions options, int cacheDuration = 3600)
        {
            var responseJson = JsonSerializer.Serialize(data, options);
            var etag = "\"" + Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes(responseJson))) + "\"";

            if (req.Headers.TryGetValue("If-None-Match", out var inm) && inm == etag)
            {
                return new StatusCodeResult(StatusCodes.Status304NotModified);
            }

            if (cacheDuration > 0)
                req.HttpContext.Response.Headers.CacheControl = $"public, max-age={cacheDuration}";
            req.HttpContext.Response.Headers.ETag = etag;

            return new ContentResult
            {
                Content = responseJson,
                StatusCode = StatusCodes.Status200OK,
                ContentType = "application/json"
            };
        }
    }
}
