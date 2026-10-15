using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services;

public sealed class ProjectService(HttpClient httpClient, SkillSnapAuthStateProvider auth)
    : ApiClientBase(httpClient, auth)
{
    public async Task<PagedResult<ProjectDto>> GetAsync(
        int page = 1, int pageSize = 25, bool all = false, CancellationToken ct = default) =>
        await GetAsync<PagedResult<ProjectDto>>($"api/projects?page={page}&pageSize={pageSize}&all={all.ToString().ToLowerInvariant()}", ct)
        ?? new PagedResult<ProjectDto>([], page, pageSize, 0);

    public Task<ProjectDto> AddAsync(CreateProjectRequest request, CancellationToken ct = default) =>
        PostAsync<CreateProjectRequest, ProjectDto>("api/projects", request, ct);

    public async Task<ProjectDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<ProjectDto>($"api/projects/{id}", ct);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<ProjectDto> UpdateAsync(int id, CreateProjectRequest request, CancellationToken ct = default) =>
        PutAsync<CreateProjectRequest, ProjectDto>($"api/projects/{id}", request, ct);
}
