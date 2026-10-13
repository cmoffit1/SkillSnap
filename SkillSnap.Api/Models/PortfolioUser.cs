using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace SkillSnap.Api.Models;

public class PortfolioUser
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; }
    public string Bio { get; set; }
    public string ProfileImageUrl { get; set; }
    
    public PortfolioUser()
    {
        Projects = new List<Project>();
        Skills = new List<Skill>();
    }

    [InverseProperty(nameof(Project.PortfolioUser))]
    public List<Project> Projects { get; set; } = new List<Project>();

    [InverseProperty(nameof(Skill.PortfolioUser))]
    public List<Skill> Skills { get; set; } = new List<Skill>();

   
}