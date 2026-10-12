using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkillSnap.Api.Models;

public class Project
{
    [Key]
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
    
    [ForeignKey(nameof(PortfolioUser))]
    public int PortfolioUserId { get; set; }
    public PortfolioUser PortfolioUser { get; set; } = null!;
}