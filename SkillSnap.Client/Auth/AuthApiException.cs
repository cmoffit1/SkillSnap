using System.Net;

namespace SkillSnap.Client.Auth;

public sealed class AuthApiException(HttpStatusCode statusCode, string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
