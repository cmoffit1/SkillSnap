using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services;

public sealed class SkillService(HttpClient httpClient, SkillSnapAuthStateProvider auth)
    : ApiClientBase(httpClient, auth)
{
    public async Task<PagedResult<SkillDto>> GetAsync(
        int page = 1, int pageSize = 25, CancellationToken ct = default) =>
        await GetAsync<PagedResult<SkillDto>>($"api/skills?page={page}&pageSize={pageSize}", ct)
        ?? new PagedResult<SkillDto>([], page, pageSize, 0);

    public Task<SkillDto> AddAsync(CreateSkillRequest request, CancellationToken ct = default) =>
        PostAsync<CreateSkillRequest, SkillDto>("api/skills", request, ct);
}
