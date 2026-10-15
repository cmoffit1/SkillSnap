using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillSnap.Api.Data;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SkillLevelsController(SkillSnapContext db, IMemoryCache cache) : ControllerBase
{
    [ProducesResponseType<IReadOnlyList<SkillLevelDto>>(200)]
    [HttpGet(Name = "GetSkillLevels")]
    public async Task<ActionResult<IReadOnlyList<SkillLevelDto>>> GetSkillLevels(CancellationToken ct)
    {
        var levels = await cache.GetOrCreateAsync("skill-levels", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return (IReadOnlyList<SkillLevelDto>)await db.SkillLevels
                .AsNoTracking()
                .OrderBy(level => level.SortOrder)
                .Select(level => new SkillLevelDto(level.Id, level.Name))
                .ToListAsync(ct);
        });

        return Ok(levels);
    }
}
