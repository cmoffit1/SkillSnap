using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services;

public sealed class SkillService(HttpClient httpClient, SkillSnapAuthStateProvider auth)
    : ApiClientBase(httpClient, auth)
{
    public async Task<PagedResult<SkillDto>> GetAsync(
        int page = 1, int pageSize = 25, bool all = false, CancellationToken ct = default) =>
        await GetAsync<PagedResult<SkillDto>>($"api/skills?page={page}&pageSize={pageSize}&all={all.ToString().ToLowerInvariant()}", ct)
        ?? new PagedResult<SkillDto>([], page, pageSize, 0);

    public async Task<IReadOnlyList<SkillLevelDto>> GetLevelsAsync(CancellationToken ct = default) =>
        await GetAsync<List<SkillLevelDto>>("api/skilllevels", ct) ?? [];

    public Task<SkillDto> AddAsync(CreateSkillRequest request, CancellationToken ct = default) =>
        PostAsync<CreateSkillRequest, SkillDto>("api/skills", request, ct);

    public async Task<SkillDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<SkillDto>($"api/skills/{id}", ct);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<SkillDto> UpdateAsync(int id, CreateSkillRequest request, CancellationToken ct = default) =>
        PutAsync<CreateSkillRequest, SkillDto>($"api/skills/{id}", request, ct);
}
