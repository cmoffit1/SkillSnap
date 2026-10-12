namespace SkillSnap.Api.Contracts.Auth;

public sealed record CurrentUserResponse(
    string UserId,
    string Email,
    IReadOnlyList<string> Roles);
