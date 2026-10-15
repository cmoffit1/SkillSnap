using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Api.Models;

/// <summary>Lookup row for the proficiency level of a skill (for example Beginner or Expert).</summary>
public class SkillLevel
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Position when levels are listed or reported; lower sorts first.</summary>
    public int SortOrder { get; set; }
}
