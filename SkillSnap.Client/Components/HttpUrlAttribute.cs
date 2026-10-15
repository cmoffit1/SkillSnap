using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Client.Components;

/// <summary>Requires an absolute http or https URL with a host; kept in step with the API's rule.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public HttpUrlAttribute() : base("The {0} field must be a valid http or https URL, such as https://example.com/image.png.")
    {
    }

    public override bool IsValid(object? value) =>
        value is not string text ||
        (Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
         uri.Host.Length > 0 &&
         text.Trim().StartsWith(uri.Scheme + "://", StringComparison.OrdinalIgnoreCase));
}
