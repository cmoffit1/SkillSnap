using Microsoft.AspNetCore.Authorization;
using SkillSnap.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using SkillSnap.Api.Models;
using SkillSnap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProjectsController(SkillSnapContext db, ListCache cache) : ControllerBase
{
    [ProducesResponseType<PagedResult<ProjectDto>>(200)]
    [HttpGet(Name = "GetProjects")]
    public async Task<ActionResult<PagedResult<ProjectDto>>> GetProjects(
        int? page, int? pageSize, bool all, CancellationToken ct)
    {
        var (p, size) = PagedResult<ProjectDto>.Normalize(page, pageSize);
        var owner = await db.GetVisibleOwnerIdAsync(User, all, ct);

        return await cache.GetOrCreateAsync(CacheSets.Projects, owner?.ToString() ?? "all", p, size, async () =>
        {
            var query = db.Projects.AsNoTracking();
            if (owner is { } ownerId)
            {
                query = query.Where(project => project.PortfolioUserId == ownerId);
            }

            var total = await query.CountAsync(ct);

            var rows = await query
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
        });
    }

    [ProducesResponseType<ProjectDto>(200)]
    [ProducesResponseType(404)]
    [HttpGet("{id}", Name = "GetProject")]
    public async Task<ActionResult<ProjectDto>> GetProjectById(int id, CancellationToken ct)
    {
        var owner = await db.GetVisibleOwnerIdAsync(User, all: true, ct);
        var query = db.Projects.AsNoTracking().Where(project => project.Id == id);
        if (owner is { } ownerId)
        {
            query = query.Where(project => project.PortfolioUserId == ownerId);
        }

        var row = await query
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
        cache.Invalidate(CacheSets.Projects);

        var dto = new ProjectDto(
            project.Id, project.Title, project.Description, project.ImageUrl, project.PortfolioUserId);

        return CreatedAtRoute("GetProject", new { id = dto.Id }, dto);
    }

    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProjectDto>(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [HttpPut("{id:int}", Name = "UpdateProject")]
    public async Task<ActionResult<ProjectDto>> UpdateProject(
        int id, CreateProjectRequest request, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return NotFound();
        }

        project.Title = request.Title;
        project.Description = request.Description;
        project.ImageUrl = request.ImageUrl;
        await db.SaveChangesAsync(ct);
        cache.Invalidate(CacheSets.Projects);

        return new ProjectDto(
            project.Id, project.Title, project.Description, project.ImageUrl, project.PortfolioUserId);
    }
}