using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SkillSnap.Client.Services;

/// <summary>
/// A failed API call with a user-friendly message. Derives from <see cref="HttpRequestException"/>
/// so existing catch blocks keep working.
/// </summary>
public sealed class ApiException(
    string userMessage,
    HttpStatusCode? statusCode = null,
    IReadOnlyList<string>? details = null,
    Exception? inner = null)
    : HttpRequestException(userMessage, inner, statusCode)
{
    public string UserMessage { get; } = userMessage;

    public IReadOnlyList<string> Details { get; } = details ?? [];

    public static async Task<ApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        List<string> details = [];
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                {
                    foreach (var message in field.Value.EnumerateArray())
                    {
                        if (message.GetString() is { } text)
                        {
                            details.Add(text);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // The body is not ProblemDetails; the status code alone is enough.
        }

        return new ApiException(MessageFor(response.StatusCode), response.StatusCode, details);
    }

    public static string MessageFor(HttpStatusCode status) => (int)status switch
    {
        400 or 422 => "Some of the information isn't valid. Please check it and try again.",
        401 => "Your session has expired. Please sign in again.",
        403 => "You don't have permission to do that.",
        404 => "We couldn't find what you were looking for.",
        409 => "This item was changed by someone else. Please reload and try again.",
        429 => "Too many requests. Please wait a moment and try again.",
        >= 500 => "The server had a problem. Please try again shortly.",
        _ => "Something went wrong. Please try again."
    };
}

public static class ApiErrors
{
    /// <summary>Returns the message to show a user for a failed API call.</summary>
    public static string ToMessage(Exception exception, string fallback) => exception switch
    {
        ApiException { Details.Count: > 0 } api => $"{api.UserMessage} {string.Join(' ', api.Details)}",
        ApiException api => api.UserMessage,
        HttpRequestException => "Can't reach the server. Check your connection and try again.",
        _ => fallback
    };
}
