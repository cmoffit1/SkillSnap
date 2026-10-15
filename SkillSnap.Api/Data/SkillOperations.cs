using System.Linq.Expressions;
using SkillSnap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Data;

public record SkillRow(int Id, string Name, int SkillLevelId, string Level, int PortfolioUserId);

public static class SkillDtoOperations
{
    public static readonly Expression<Func<Skill, SkillRow>> ToRow = skill => new SkillRow(
            skill.Id,
            skill.Name,
            skill.SkillLevelId,
            skill.SkillLevel.Name,
            skill.PortfolioUserId);

    public static SkillDto ToDto(this SkillRow row) =>
        new(
            row.Id,
            row.Name,
            row.SkillLevelId,
            row.Level,
            row.PortfolioUserId);
}
