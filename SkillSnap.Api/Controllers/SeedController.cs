using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Auth;
using SkillSnap.Api.Data;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/[controller]")]
public class SeedController(SkillSnapContext db, ListCache cache) : ControllerBase
{
    [ProducesResponseType<string>(200)]
    [ProducesResponseType(400)]
    [HttpPost(Name = "SeedSampleData")]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        if (await db.PortfolioUsers.AnyAsync(ct))
        {
            return BadRequest("Sample data already exists.");
        }

        var user = new PortfolioUser
        {
            Name = "Jordan Developer",
            Bio = "Full-stack developer passionate about learning new tech.",
            ProfileImageUrl = "https://example.com/images/jordan.png",
            Projects =
            [
                new Project { Title = "Task Tracker", Description = "Manage tasks effectively", ImageUrl = "https://example.com/images/task.png" },
                new Project { Title = "Weather App", Description = "Forecast weather using APIs", ImageUrl = "https://example.com/images/weather.png" }
            ],
            Skills =
            [
                new Skill { Name = "C#", SkillLevelId = 3 },
                new Skill { Name = "Blazor", SkillLevelId = 2 }
            ]
        };

        db.PortfolioUsers.Add(user);
        await db.SaveChangesAsync(ct);
        cache.Invalidate(CacheSets.Projects);
        cache.Invalidate(CacheSets.Skills);

        return Ok("Sample data inserted.");
    }
}
