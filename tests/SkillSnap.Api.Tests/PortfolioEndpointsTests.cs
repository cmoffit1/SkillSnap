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
            ImageUrl = "https://example.test/extra.png",
            PortfolioUserId = portfolioUser.Id
        });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var project = await projectResponse.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.NotNull(project);

        using var skillResponse = await client.PostAsJsonAsync("/api/skills", new
        {
            Name = "C#",
            Level = "Advanced",
            PortfolioUserId = portfolioUser.Id
        });
        Assert.Equal(HttpStatusCode.Created, skillResponse.StatusCode);
        var skill = await skillResponse.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(skill);

        using var pagedProjectsResponse = await client.GetAsync("/api/projects?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, pagedProjectsResponse.StatusCode);
        var pagedProjects = await pagedProjectsResponse.Content.ReadFromJsonAsync<PagedResult<ProjectDto>>();
        Assert.NotNull(pagedProjects);
        Assert.Single(pagedProjects.Items);
        Assert.Equal(3, pagedProjects.TotalCount);

        using var getProjectResponse = await client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, getProjectResponse.StatusCode);
        using var getSkillResponse = await client.GetAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.OK, getSkillResponse.StatusCode);
    }
}
