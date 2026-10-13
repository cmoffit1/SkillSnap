using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSnap.Api.Models;
using SkillSnap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PortfolioUserController(SkillSnapContext db) : ControllerBase
{
    [ProducesResponseType<PagedResult<PortfolioUserDto>>(200)]
    [HttpGet(Name = "GetPortfolioUsers")]
    public async Task<ActionResult<PagedResult<PortfolioUserDto>>> GetAll(
        int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = PagedResult<PortfolioUserDto>.Normalize(page, pageSize);
        var total = await db.PortfolioUsers.CountAsync(ct);

        var rows = await db.PortfolioUsers
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(PortfolioUserOperations.ToRow)
            .ToListAsync(ct);

        return new PagedResult<PortfolioUserDto>(rows.Select(PortfolioUserOperations.ToDto).ToList(), p, size, total);
    }

    [ProducesResponseType<PortfolioUserDto>(200)]
    [ProducesResponseType(404)]
    [HttpGet("{id:int}", Name = "GetPortfolioUser")]
    public async Task<ActionResult<PortfolioUserDto>> GetById(int id, CancellationToken ct)
    {
        var row = await db.PortfolioUsers
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(PortfolioUserOperations.ToRow)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? NotFound()
            : PortfolioUserOperations.ToDto(row);
    }

    [ProducesResponseType<PortfolioUserDto>(201)]
    [HttpPost(Name = "CreatePortfolioUser")]
    public async Task<ActionResult<PortfolioUserDto>> Create(
        CreatePortfolioUserRequest request, CancellationToken ct)
    {
        var user = new PortfolioUser
        {
            Name = request.Name,
            Bio = request.Bio,
            ProfileImageUrl = request.ProfileImageUrl,
            Projects = request.Projects
                .Select(project => new Project
                {
                    Title = project.Title,
                    Description = project.Description,
                    ImageUrl = project.ImageUrl
                })
                .ToList(),
            Skills = request.Skills
                .Select(skill => new Skill
                {
                    Name = skill.Name,
                    Level = skill.Level
                })
                .ToList()
        };

        db.PortfolioUsers.Add(user);
        await db.SaveChangesAsync(ct);

        var row = await db.PortfolioUsers
            .AsNoTracking()
            .Where(savedUser => savedUser.Id == user.Id)
            .Select(PortfolioUserOperations.ToRow)
            .SingleAsync(ct);
        var dto = PortfolioUserOperations.ToDto(row);

        return CreatedAtRoute("GetPortfolioUser", new { id = dto.Id }, dto);
    }
}
