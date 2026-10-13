using System.Linq.Expressions;
using SkillSnap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Data;

public record SkillRow(int Id, string Name, string Level, int PortfolioUserId);

public static class SkillDtoOperations
{
    public static readonly Expression<Func<Skill, SkillRow>> ToRow = skill => new SkillRow(
            skill.Id, 
            skill.Name, 
            skill.Level,
            skill.PortfolioUserId);

    public static SkillDto ToDto(this SkillRow row) =>
        new(
            row.Id,
            row.Name,
            row.Level,
            row.PortfolioUserId);
    
}