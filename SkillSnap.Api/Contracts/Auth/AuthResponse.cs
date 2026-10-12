namespace SkillSnap.Api.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresUtc,
    string UserId,
    string Email,
    IReadOnlyList<string> Roles);
