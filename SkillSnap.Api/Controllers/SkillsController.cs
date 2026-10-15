using Microsoft.AspNetCore.Authorization;
using SkillSnap.Api.Auth;
using SkillSnap.Api.Data;
using SkillSnap.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SkillsController(SkillSnapContext db, ListCache cache, SkillLevelCache levels) : ControllerBase
{
    [ProducesResponseType<PagedResult<SkillDto>>(200)]
    [HttpGet(Name = "GetSkills")]
    public async Task<ActionResult<PagedResult<SkillDto>>> GetSkills(
        int? page, int? pageSize, bool all, CancellationToken ct)
    {
        var (p, size) = PagedResult<SkillDto>.Normalize(page, pageSize);
        var owner = await db.GetVisibleOwnerIdAsync(User, all, cache, ct);

        return await cache.GetOrCreateAsync(CacheSets.Skills, owner?.ToString() ?? "all", p, size, async () =>
        {
            var query = db.Skills.AsNoTracking();
            if (owner is { } ownerId)
            {
                query = query.Where(skill => skill.PortfolioUserId == ownerId);
            }

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderBy(skill => skill.Id)
                .Skip((p - 1) * size)
                .Take(size)
                .Select(SkillDtoOperations.ToRow)
                .ToListAsync(ct);

            return new PagedResult<SkillDto>(
                rows.Select(SkillDtoOperations.ToDto).ToList(),
                p,
                size,
                total);
        });
    }

    [ProducesResponseType<SkillDto>(200)]
    [ProducesResponseType(404)]
    [HttpGet("{id}", Name = "GetSkill")]
    public async Task<ActionResult<SkillDto>> GetSkillById(int id, CancellationToken ct)
    {
        var owner = await db.GetVisibleOwnerIdAsync(User, all: true, cache, ct);
        var query = db.Skills.AsNoTracking().Where(skill => skill.Id == id);
        if (owner is { } ownerId)
        {
            query = query.Where(skill => skill.PortfolioUserId == ownerId);
        }

        var row = await query
            .Select(SkillDtoOperations.ToRow)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? NotFound()
            : SkillDtoOperations.ToDto(row);
    }

    [ProducesResponseType<SkillDto>(201)]
    [ProducesResponseType(400)]
    [HttpPost(Name = "CreateSkill")]
    public async Task<ActionResult<SkillDto>> CreateSkill(
        CreateSkillLineRequest request, CancellationToken ct)
    {
        var owner = await db.GetOrCreateOwnerAsync(User, ct);
        if (owner is null)
        {
            return Unauthorized();
        }

        var levelName = await FindLevelNameAsync(request.SkillLevelId, ct);
        if (levelName is null)
        {
            return InvalidLevel();
        }

        var skill = new Skill
        {
            Name = request.Name,
            SkillLevelId = request.SkillLevelId,
            PortfolioUser = owner
        };

        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);
        cache.Invalidate(CacheSets.Skills);

        var dto = new SkillDto(skill.Id, skill.Name, skill.SkillLevelId, levelName, skill.PortfolioUserId);

        return CreatedAtRoute("GetSkill", new { id = dto.Id }, dto);
    }

    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SkillDto>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("{id:int}", Name = "UpdateSkill")]
    public async Task<ActionResult<SkillDto>> UpdateSkill(
        int id, CreateSkillLineRequest request, CancellationToken ct)
    {
        var skill = await db.Skills.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (skill is null)
        {
            return NotFound();
        }

        var levelName = await FindLevelNameAsync(request.SkillLevelId, ct);
        if (levelName is null)
        {
            return InvalidLevel();
        }

        skill.Name = request.Name;
        skill.SkillLevelId = request.SkillLevelId;
        await db.SaveChangesAsync(ct);
        cache.Invalidate(CacheSets.Skills);

        return new SkillDto(skill.Id, skill.Name, skill.SkillLevelId, levelName, skill.PortfolioUserId);
    }

    private async Task<string?> FindLevelNameAsync(int levelId, CancellationToken ct) =>
        (await levels.GetAsync(db, ct)).FirstOrDefault(level => level.Id == levelId)?.Name;

    private ActionResult InvalidLevel() =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["SkillLevelId"] = ["Unknown skill level."]
        }));
}