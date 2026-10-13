using System.ComponentModel.DataAnnotations;

namespace SkillSnap.Api.Models;

/// <summary>
/// Represents a project with its ID, title, description, image URL, and associated portfolio user ID.
/// </summary>
/// <param name="Id">The unique identifier of the project.</param>
/// <param name="Title">The title of the project.</param>
/// <param name="Description">The description of the project.</param>
/// <param name="ImageUrl">The image URL of the project.</param>
/// <param name="PortfolioUserId">The ID of the portfolio user associated with the project.</param>
public record ProjectDto(int Id, string Title, string Description, string ImageUrl, int PortfolioUserId);

/// <summary>
/// Represents a skill with its ID, name, level, and associated portfolio user ID.
/// </summary>
/// <param name="Id">The unique identifier of the skill.</param>
/// <param name="Name">The name of the skill.</param>
/// <param name="Level">The level of the skill.</param>
/// <param name="PortfolioUserId">The ID of the portfolio user associated with the skill.</param>
public record SkillDto(int Id, string Name, string Level, int PortfolioUserId);

/// <summary>
/// Represents a portfolio user with its profile details, projects, and skills.
/// </summary>
/// <param name="Id">The unique identifier of the portfolio user.</param>
/// <param name="Name">The name of the portfolio user.</param>
/// <param name="Bio">The bio of the portfolio user.</param>
/// <param name="ProfileImageUrl">The profile image URL of the portfolio user.</param>
/// <param name="Projects">The list of projects associated with the portfolio user.</param>
/// <param name="Skills">The list of skills associated with the portfolio user.</param>
public record PortfolioUserDto(
    int Id,
    string Name,
    string Bio,
    string ProfileImageUrl,
    List<ProjectDto> Projects,
    List<SkillDto> Skills);

/// <summary>
/// Represents a request to create a new project with its title, description, image URL, and associated portfolio user ID.
/// </summary>
/// <param name="Title">The title of the project.</param>
/// <param name="Description">The description of the project.</param>
/// <param name="ImageUrl">The image URL of the project.</param>
/// <param name="PortfolioUserId">The ID of the portfolio user associated with the project.</param>
public record CreateProjectRequest(
    [Required, NotWhitespace, StringLength(100)] string Title,
    [Required, NotWhitespace, StringLength(500)] string Description,
    [Required, NotWhitespace, StringLength(200)] string ImageUrl,
    [Range(1, int.MaxValue)] int PortfolioUserId);

/// <summary>
/// Represents a request to create a new skill line with its name, level, and associated portfolio user ID. 
/// </summary>
/// <param name="Name">The name of the skill line.</param>
/// <param name="Level">The level of the skill line.</param>
/// <param name="PortfolioUserId">The ID of the portfolio user associated with the skill line.</param>
public record CreateSkillLineRequest(
    [Required, NotWhitespace, StringLength(100)] string Name,
    [Required, NotWhitespace, StringLength(50)] string Level,
    [Range(1, int.MaxValue)] int PortfolioUserId);

/// <summary>
/// Represents a project included when creating a portfolio user. The parent ID is assigned by the server.
/// </summary>
/// <param name="Title">The title of the project.</param>
/// <param name="Description">The description of the project.</param>
/// <param name="ImageUrl">The image URL of the project.</param>
public record CreatePortfolioProjectRequest(
    [Required, NotWhitespace, StringLength(100)] string Title,
    [Required, NotWhitespace, StringLength(500)] string Description,
    [Required, NotWhitespace, StringLength(200)] string ImageUrl);

/// <summary>
/// Represents a skill included when creating a portfolio user. The parent ID is assigned by the server.
/// </summary>
/// <param name="Name">The skill name.</param>
/// <param name="Level">The skill level.</param>
public record CreatePortfolioSkillRequest(
    [Required, NotWhitespace, StringLength(100)] string Name,
    [Required, NotWhitespace, StringLength(50)] string Level);

/// <summary>
/// Represents a request to create a portfolio user with its profile details, projects, and skills.
/// </summary>
/// <param name="Name">The name of the portfolio user.</param>
/// <param name="Bio">The bio of the portfolio user.</param>
/// <param name="ProfileImageUrl">The profile image URL of the portfolio user.</param>
/// <param name="Projects">The projects to create for the portfolio user.</param>
/// <param name="Skills">The skills to create for the portfolio user.</param>
public record CreatePortfolioUserRequest(
    [Required, NotWhitespace, StringLength(100)] string Name,
    [Required, NotWhitespace, StringLength(500)] string Bio,
    [Required, NotWhitespace, StringLength(200)] string ProfileImageUrl,
    [Required] List<CreatePortfolioProjectRequest> Projects,
    [Required] List<CreatePortfolioSkillRequest> Skills);

/// <summary>Page envelope returned by list endpoints, with supported paging limits.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>
    /// Applies the default page size and clamps page inputs so the resulting Skip offset fits in an Int32.
    /// </summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var maxPage = (int)Math.Min(
            (long)int.MaxValue / normalizedPageSize + 1,
            int.MaxValue);
        var normalizedPage = Math.Clamp(page ?? 1, 1, maxPage);

        return (normalizedPage, normalizedPageSize);
    }
}