using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Data;

public static class PortfolioOwnership
{
    /// <summary>
    /// Returns the caller's portfolio, creating and linking an empty one on first use.
    /// Changes are tracked but not saved, so the caller commits them with the new project or skill.
    /// Returns null when the token does not map to a known account.
    /// </summary>
    public static async Task<PortfolioUser?> GetOrCreateOwnerAsync(
        this SkillSnapContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                     principal.FindFirstValue("sub");
        if (userId is null)
        {
            return null;
        }

        var account = await db.Users.SingleOrDefaultAsync(user => user.Id == userId, ct);
        if (account is null)
        {
            return null;
        }

        if (account.PortfolioUserId is { } portfolioUserId)
        {
            var existing = await db.PortfolioUsers.FindAsync([portfolioUserId], ct);
            if (existing is not null)
            {
                return existing;
            }
        }

        var email = account.Email ?? account.UserName ?? "SkillSnap user";
        var owner = new PortfolioUser
        {
            Name = email.Split('@')[0],
            Bio = string.Empty,
            ProfileImageUrl = string.Empty
        };

        db.PortfolioUsers.Add(owner);
        account.PortfolioUser = owner;
        return owner;
    }
}
