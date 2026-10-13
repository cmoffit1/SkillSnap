namespace SkillSnap.Client.Components.Portfolio;

public sealed record ProjectDisplayModel(string Title, string Description, string? ImageUrl = null);

public sealed record SkillDisplayModel(string Name, string? Level = null);
