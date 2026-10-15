namespace SkillSnap.Client.Services;

public record ProjectDto(int Id, string Title, string Description, string ImageUrl, int PortfolioUserId);

public record SkillDto(int Id, string Name, string Level, int PortfolioUserId);

public record CreateProjectRequest(string Title, string Description, string ImageUrl);

public record CreateSkillRequest(string Name, string Level);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
