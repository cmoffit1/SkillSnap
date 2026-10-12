namespace SkillSnap.Api.Auth;

public sealed class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
}
