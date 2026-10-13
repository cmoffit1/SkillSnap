using System.Linq.Expressions;
using SkillSnap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Data;

public record ProjectRow(int Id, string Title, string Description, string ImageUrl, int PortfolioUserId);

public static class ProjectDtoOperations
{
    public static readonly Expression<Func<Project, ProjectRow>> ToRow = project => new ProjectRow(
            project.Id, 
            project.Title, 
            project.Description,
            project.ImageUrl,
            project.PortfolioUserId);

    public static ProjectDto ToDto(this ProjectRow row) =>
        new(
            row.Id,
            row.Title,
            row.Description,
            row.ImageUrl,
            row.PortfolioUserId);
    
}