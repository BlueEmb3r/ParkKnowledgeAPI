using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace ParkKnowledgeAPI.Tests.Helpers;

public static class HttpRequestHelper
{
    public static HttpRequest CreateJsonRequest<T>(T body)
    {
        var json = JsonSerializer.Serialize(body);
        var bytes = Encoding.UTF8.GetBytes(json);
        var stream = new MemoryStream(bytes);

        var context = new DefaultHttpContext();
        context.Request.Body = stream;
        context.Request.ContentLength = bytes.Length;
        context.Request.ContentType = "application/json";
        // Use a writable MemoryStream for the response body so tests can read it
        context.Response.Body = new MemoryStream();

        return context.Request;
    }

    public static HttpRequest CreateEmptyRequest()
    {
        // Send JSON "null" so ReadFromJsonAsync returns null instead of throwing
        // on missing content-type
        var bytes = Encoding.UTF8.GetBytes("null");
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Request.ContentType = "application/json";
        context.Response.Body = new MemoryStream();

        return context.Request;
    }
}
