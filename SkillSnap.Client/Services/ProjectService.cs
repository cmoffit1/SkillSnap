using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services;

public sealed class ProjectService(HttpClient httpClient, SkillSnapAuthStateProvider auth)
    : ApiClientBase(httpClient, auth)
{
    public async Task<PagedResult<ProjectDto>> GetAsync(
        int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        await GetAsync<PagedResult<ProjectDto>>($"api/projects?page={page}&pageSize={pageSize}", ct)
        ?? new PagedResult<ProjectDto>([], page, pageSize, 0);

    public Task<ProjectDto> AddAsync(CreateProjectRequest request, CancellationToken ct = default) =>
        PostAsync<CreateProjectRequest, ProjectDto>("api/projects", request, ct);
}
