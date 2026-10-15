using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SkillSnap.Api.Contracts.Auth;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Tests;

public sealed class PortfolioEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Portfolio_project_and_skill_endpoints_support_paging_and_creation()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var anonymousResponse = await client.GetAsync("/api/projects");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        using var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            Email = $"portfolio-{Guid.NewGuid():N}@example.test",
            Password = "VeryStrong!Password123"
        });
        Assert.True(
            registerResponse.IsSuccessStatusCode,
            await registerResponse.Content.ReadAsStringAsync());

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        using var createUserResponse = await client.PostAsJsonAsync("/api/portfoliouser", new
        {
            Name = "Test User",
            Bio = "Portfolio test",
            ProfileImageUrl = "https://example.test/profile.png",
            Projects = new[]
            {
                new { Title = "Zebra", Description = "Second", ImageUrl = "https://example.test/z.png" },
                new { Title = "Alpha", Description = "First", ImageUrl = "https://example.test/a.png" }
            },
            Skills = new[]
            {
                new { Name = "Zig", Level = "Advanced" },
                new { Name = "Angular", Level = "Intermediate" }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        var portfolioUser = await createUserResponse.Content.ReadFromJsonAsync<PortfolioUserDto>();
        Assert.NotNull(portfolioUser);
        Assert.Equal(["Alpha", "Zebra"], portfolioUser.Projects.Select(project => project.Title).ToArray());
        Assert.Equal(["Angular", "Zig"], portfolioUser.Skills.Select(skill => skill.Name).ToArray());

        using var getUserResponse = await client.GetAsync($"/api/portfoliouser/{portfolioUser.Id}");
        Assert.Equal(HttpStatusCode.OK, getUserResponse.StatusCode);

        using var projectResponse = await client.PostAsJsonAsync("/api/projects", new
        {
            Title = "Additional project",
            Description = "Created directly",
            ImageUrl = "https://example.test/extra.png"
        });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        using var skillResponse = await client.PostAsJsonAsync("/api/skills", new
        {
            Name = "C#",
            Level = "Advanced"
        });
        Assert.Equal(HttpStatusCode.Created, skillResponse.StatusCode);
        var skill = await skillResponse.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(skill);

        using var pagedProjectsResponse = await client.GetAsync("/api/projects?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, pagedProjectsResponse.StatusCode);
        var pagedProjects = await pagedProjectsResponse.Content.ReadFromJsonAsync<PagedResult<ProjectDto>>();
        Assert.NotNull(pagedProjects);
        Assert.Single(pagedProjects.Items);
        Assert.True(pagedProjects.TotalCount >= 3);

        using var getProjectResponse = await client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, getProjectResponse.StatusCode);
        using var getSkillResponse = await client.GetAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.OK, getSkillResponse.StatusCode);
    }

    [Fact]
    public async Task Created_items_belong_to_the_callers_own_portfolio()
    {
        using var anonymous = CreateClient();
        using var anonymousPost = await anonymous.PostAsJsonAsync("/api/skills", new { Name = "C#", Level = "Advanced" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);

        using var first = await CreateSignedInClientAsync();
        using var second = await CreateSignedInClientAsync();

        var firstSkill = await AddSkillAsync(first, "First skill");
        var firstAgain = await AddSkillAsync(first, "First again");
        var secondSkill = await AddSkillAsync(second, "Second skill");

        Assert.Equal(firstSkill.PortfolioUserId, firstAgain.PortfolioUserId);
        Assert.NotEqual(firstSkill.PortfolioUserId, secondSkill.PortfolioUserId);

        using var projectResponse = await first.PostAsJsonAsync("/api/projects", new
        {
            Title = "Mine",
            Description = "Owned",
            ImageUrl = "https://example.test/mine.png"
        });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(firstSkill.PortfolioUserId, project!.PortfolioUserId);
    }

    private static async Task<SkillDto> AddSkillAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/skills", new { Name = name, Level = "Advanced" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SkillDto>())!;
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<HttpClient> CreateSignedInClientAsync()
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/auth/register", new
        {
            Email = $"owner-{Guid.NewGuid():N}@example.test",
            Password = "VeryStrong!Password123"
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
