using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Data;

/// <summary>
/// Lightweight representation of a portfolio user. used for EF Projections before mapping to dto.  
/// </summary>
/// <param name="Id"></param>
/// <param name="Name"></param>
/// <param name="Bio"></param>
/// <param name="ProfileImageUrl"></param>
/// <param name="Projects"></param>
/// <param name="Skills"></param>
public record PortfolioUserRow(
    int Id,
    string Name,
    string Bio,
    string ProfileImageUrl,
    List<ProjectRow> Projects,
    List<SkillRow> Skills);

public static class PortfolioUserOperations
{
    public static readonly Expression<Func<PortfolioUser, PortfolioUserRow>> ToRow =
        user => new PortfolioUserRow(
            user.Id,
            user.Name,
            user.Bio,
            user.ProfileImageUrl,
            user.Projects
                .OrderBy(project => project.Title)
                .Select(project => new ProjectRow(
                    project.Id,
                    project.Title,
                    project.Description,
                    project.ImageUrl,
                    project.PortfolioUserId))
                .ToList(),
            user.Skills
                .OrderBy(skill => skill.Name)
                .Select(skill => new SkillRow(
                    skill.Id,
                    skill.Name,
                    skill.SkillLevelId,
                    skill.SkillLevel.Name,
                    skill.PortfolioUserId))
                .ToList());


    public static PortfolioUserDto ToDto(this PortfolioUserRow row) =>
        new(
            row.Id,
            row.Name,
            row.Bio,
            row.ProfileImageUrl,
            row.Projects
                .Select(project => new ProjectDto(
                    project.Id,
                    project.Title,
                    project.Description,
                    project.ImageUrl,
                    project.PortfolioUserId))
                .ToList(),
            row.Skills
                .Select(skill => new SkillDto(
                    skill.Id,
                    skill.Name,
                    skill.SkillLevelId,
                    skill.Level,
                    skill.PortfolioUserId))
                .ToList());
}