using Microsoft.AspNetCore.Authorization;
using SkillSnap.Api.Data;
using SkillSnap.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SkillsController(SkillSnapContext db) : ControllerBase
{
    [ProducesResponseType<PagedResult<SkillDto>>(200)]
    [HttpGet(Name = "GetSkills")]
    public async Task<ActionResult<PagedResult<SkillDto>>> GetSkills(
        int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = PagedResult<SkillDto>.Normalize(page, pageSize);
        var total = await db.Skills.CountAsync(ct);

        var rows = await db.Skills
            .AsNoTracking()
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
    }

    [ProducesResponseType<SkillDto>(200)]
    [ProducesResponseType(404)]
    [HttpGet("{id}", Name = "GetSkill")]
    public async Task<ActionResult<SkillDto>> GetSkillById(int id, CancellationToken ct)
    {
        var row = await db.Skills
            .AsNoTracking()
            .Where(skill => skill.Id == id)
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
        var portfolioUserExists = await db.PortfolioUsers
            .AnyAsync(user => user.Id == request.PortfolioUserId, ct);
        if (!portfolioUserExists)
        {
            ModelState.AddModelError(
                nameof(request.PortfolioUserId),
                "The specified portfolio user does not exist.");
            return ValidationProblem(ModelState);
        }

        var skill = new Skill
        {
            Name = request.Name,
            Level = request.Level,
            PortfolioUserId = request.PortfolioUserId
        };

        db.Skills.Add(skill);
        await db.SaveChangesAsync(ct);

        var row = await db.Skills
            .AsNoTracking()
            .Where(savedSkill => savedSkill.Id == skill.Id)
            .Select(SkillDtoOperations.ToRow)
            .SingleAsync(ct);
        var dto = SkillDtoOperations.ToDto(row);

        return CreatedAtRoute("GetSkill", new { id = dto.Id }, dto);
    }
}