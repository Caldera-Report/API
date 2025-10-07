extern alias APIAssembly;

using APIAssembly::API.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace API.Tests.Helpers;

public class ResponseHelpersTests
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CachedJson_ReturnsContentResultAndSetsCachingHeaders()
    {
        var context = new DefaultHttpContext();
        var request = context.Request;
        var data = new[] { new { Value = 1 } };

        var result = ResponseHelpers.CachedJson(request, data, _jsonOptions, 120);

        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, contentResult.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(data, _jsonOptions), contentResult.Content);
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.Equal("public, max-age=120", context.Response.Headers.CacheControl.ToString());
        Assert.False(string.IsNullOrEmpty(context.Response.Headers.ETag));
    }

    [Fact]
    public void CachedJson_ReturnsNotModifiedWhenIfNoneMatchMatches()
    {
        var data = new[] { "value" };
        var serialized = JsonSerializer.Serialize(data, _jsonOptions);
        var expectedEtag = "\"" + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(serialized))) + "\"";

        var context = new DefaultHttpContext();
        context.Request.Headers["If-None-Match"] = expectedEtag;

        var result = ResponseHelpers.CachedJson(context.Request, data, _jsonOptions, 60);

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.CacheControl));
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.ETag));
    }
}
