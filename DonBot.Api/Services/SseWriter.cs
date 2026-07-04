using System.Text.Json;

namespace DonBot.Api.Services;

public static class SseWriter
{
    public static void Prepare(HttpResponse response)
    {
        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
    }

    public static async Task WriteDataAsync(
        HttpResponse response,
        string data,
        CancellationToken ct = default)
    {
        await response.WriteAsync($"data: {data}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    public static async Task WriteJsonEventAsync(
        HttpResponse response,
        string eventName,
        object payload,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, jsonOptions);
        await response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    public static async Task WriteCommentAsync(
        HttpResponse response,
        string comment,
        CancellationToken ct = default)
    {
        await response.WriteAsync($": {comment}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
