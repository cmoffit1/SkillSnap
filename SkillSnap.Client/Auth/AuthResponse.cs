namespace SkillSnap.Client.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresUtc,
    string UserId,
    string Email,
    string[] Roles);
