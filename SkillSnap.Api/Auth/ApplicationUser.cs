using Microsoft.AspNetCore.Identity;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Auth;

public sealed class ApplicationUser : IdentityUser
{
    public int? PortfolioUserId { get; set; }
    public PortfolioUser? PortfolioUser { get; set; }
}
