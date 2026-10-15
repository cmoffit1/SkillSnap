using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillSnap.Api.Models;
using SkillSnap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectsController(SkillSnapContext db) : ControllerBase
{
    [ProducesResponseType<PagedResult<ProjectDto>>(200)]
    [HttpGet(Name = "GetProjects")]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetProjects(
        int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = PagedResult<ProjectDto>.Normalize(page, pageSize);
        var total = await db.Projects.CountAsync(ct);

        var rows = await db.Projects
            .AsNoTracking()
            .OrderBy(project => project.Id)
            .Skip((p - 1) * size)
            .Take(size)
            .Select(ProjectDtoOperations.ToRow)
            .ToListAsync(ct);

        return new PagedResult<ProjectDto>(
            rows.Select(ProjectDtoOperations.ToDto).ToList(),
            p,
            size,
            total);
    }

    [ProducesResponseType<ProjectDto>(200)]
    [ProducesResponseType(404)]
    [HttpGet("{id}", Name = "GetProject")]
    public async Task<ActionResult<ProjectDto>> GetProjectById(int id, CancellationToken ct)
    {
        var row = await db.Projects
            .AsNoTracking()
            .Where(project => project.Id == id)
            .Select(ProjectDtoOperations.ToRow)
            .FirstOrDefaultAsync(ct);

        return row is null
            ? NotFound()
            : ProjectDtoOperations.ToDto(row);
    }

    [ProducesResponseType<ProjectDto>(201)]
    [ProducesResponseType(400)]
    [HttpPost(Name = "CreateProject")]
    public async Task<ActionResult<ProjectDto>> CreateProject(
        CreateProjectRequest request, CancellationToken ct)
    {
        var owner = await db.GetOrCreateOwnerAsync(User, ct);
        if (owner is null)
        {
            return Unauthorized();
        }

        var project = new Project
        {
            Title = request.Title,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            PortfolioUser = owner
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        var row = await db.Projects
            .AsNoTracking()
            .Where(savedProject => savedProject.Id == project.Id)
            .Select(ProjectDtoOperations.ToRow)
            .SingleAsync(ct);
        var dto = ProjectDtoOperations.ToDto(row);

        return CreatedAtRoute("GetProject", new { id = dto.Id }, dto);
    }
}