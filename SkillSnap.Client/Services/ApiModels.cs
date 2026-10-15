namespace SkillSnap.Client.Services;

public record ProjectDto(int Id, string Title, string Description, string ImageUrl, int PortfolioUserId);

public record SkillDto(int Id, string Name, int SkillLevelId, string Level, int PortfolioUserId);

public record SkillLevelDto(int Id, string Name);

public record CreateProjectRequest(string Title, string Description, string ImageUrl);

public record CreateSkillRequest(string Name, int SkillLevelId);

public record PortfolioProfileDto(int Id, string Name, string Bio, string ProfileImageUrl);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
