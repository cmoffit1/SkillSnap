using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services;

public sealed class PortfolioService(HttpClient httpClient, SkillSnapAuthStateProvider auth)
    : ApiClientBase(httpClient, auth)
{
    /// <summary>The signed-in user's portfolio banner, or null when they have no portfolio yet.</summary>
    public async Task<PortfolioProfileDto?> GetMineAsync(CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<PortfolioProfileDto>("api/portfoliouser/me", ct);
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
