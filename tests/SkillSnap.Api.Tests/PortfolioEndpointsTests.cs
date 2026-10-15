using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SkillSnap.Api.Auth;
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
                new { Name = "Zig", SkillLevelId = 3 },
                new { Name = "Angular", SkillLevelId = 2 }
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
            SkillLevelId = 3
        });
        Assert.Equal(HttpStatusCode.Created, skillResponse.StatusCode);
        var skill = await skillResponse.Content.ReadFromJsonAsync<SkillDto>();
        Assert.NotNull(skill);

        using var pagedProjectsResponse = await client.GetAsync("/api/projects?page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, pagedProjectsResponse.StatusCode);
        var pagedProjects = await pagedProjectsResponse.Content.ReadFromJsonAsync<PagedResult<ProjectDto>>();
        Assert.NotNull(pagedProjects);
        Assert.Single(pagedProjects.Items);
        Assert.Equal(1, pagedProjects.TotalCount);

        using var getProjectResponse = await client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, getProjectResponse.StatusCode);
        using var getSkillResponse = await client.GetAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.OK, getSkillResponse.StatusCode);
    }

    [Fact]
    public async Task Created_items_belong_to_the_callers_own_portfolio()
    {
        using var anonymous = CreateClient();
        using var anonymousPost = await anonymous.PostAsJsonAsync("/api/skills", new { Name = "C#", SkillLevelId = 3 });
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

    [Fact]
    public async Task Cached_lists_are_refreshed_after_a_create()
    {
        using var client = await CreateSignedInClientAsync();

        var before = await client.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=1");
        var cachedAgain = await client.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=1");
        Assert.Equal(before!.TotalCount, cachedAgain!.TotalCount);

        await AddSkillAsync(client, "Cache buster");

        var after = await client.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=1");
        Assert.Equal(before.TotalCount + 1, after!.TotalCount);

        var projectsBefore = await client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?page=1&pageSize=1");
        using var projectResponse = await client.PostAsJsonAsync("/api/projects", new
        {
            Title = "Cache project",
            Description = "Invalidates",
            ImageUrl = "https://example.test/c.png"
        });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var projectsAfter = await client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects?page=1&pageSize=1");
        Assert.Equal(projectsBefore!.TotalCount + 1, projectsAfter!.TotalCount);
    }

    [Fact]
    public async Task Users_see_only_their_own_records_and_admins_can_choose_to_see_all()
    {
        using var owner = await CreateSignedInClientAsync();
        using var other = await CreateSignedInClientAsync();
        var ownerSkill = await AddSkillAsync(owner, "Owner only");
        var otherSkill = await AddSkillAsync(other, "Other only");

        var mine = await owner.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=100");
        Assert.Equal([ownerSkill.Id], mine!.Items.Select(s => s.Id).ToArray());

        // Asking for everything is ignored for non-admins.
        var ignored = await owner.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?all=true&page=1&pageSize=100");
        Assert.Equal([ownerSkill.Id], ignored!.Items.Select(s => s.Id).ToArray());

        using var hidden = await owner.GetAsync($"/api/skills/{otherSkill.Id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var newcomer = await CreateSignedInClientAsync();
        var none = await newcomer.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=100");
        Assert.Empty(none!.Items);

        using var admin = await CreateSignedInClientAsync($"admin-{Guid.NewGuid():N}@example.test", makeAdmin: true);
        var adminMine = await admin.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=100");
        Assert.Empty(adminMine!.Items);
        var everyone = await admin.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?all=true&page=1&pageSize=100");
        Assert.Contains(everyone!.Items, s => s.Id == ownerSkill.Id);
        Assert.Contains(everyone.Items, s => s.Id == otherSkill.Id);
        using var adminGet = await admin.GetAsync($"/api/skills/{otherSkill.Id}");
        Assert.Equal(HttpStatusCode.OK, adminGet.StatusCode);
    }

    [Fact]
    public async Task Project_image_urls_must_be_http_or_https()
    {
        using var client = await CreateSignedInClientAsync();

        foreach (var bad in new[] { "https:/mynonexistent.img", "javascript:alert(1)", "not a url", "ftp://example.test/a.png" })
        {
            using var response = await client.PostAsJsonAsync("/api/projects", new { Title = "T", Description = "D", ImageUrl = bad });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        using var ok = await client.PostAsJsonAsync("/api/projects", new { Title = "T", Description = "D", ImageUrl = "https://example.test/a.png" });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_callers_portfolio_banner()
    {
        using var client = await CreateSignedInClientAsync();
        using var none = await client.GetAsync("/api/portfoliouser/me");
        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);

        await AddSkillAsync(client, "Banner skill");
        var me = await client.GetFromJsonAsync<PortfolioProfileDto>("/api/portfoliouser/me");
        Assert.False(string.IsNullOrWhiteSpace(me!.Name));

        using var anonymous = CreateClient();
        using var denied = await anonymous.GetAsync("/api/portfoliouser/me");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    [Fact]
    public async Task Only_admins_can_delete_projects_and_skills()
    {
        using var staff = await CreateSignedInClientAsync();
        var skill = await AddSkillAsync(staff, "To delete");
        using var projectResponse = await staff.PostAsJsonAsync("/api/projects", new
        {
            Title = "To delete",
            Description = "Doomed",
            ImageUrl = "https://example.test/d.png"
        });
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;

        using var staffProject = await staff.DeleteAsync($"/api/projects/{project.Id}");
        using var staffSkill = await staff.DeleteAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, staffProject.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staffSkill.StatusCode);

        using var anonymous = CreateClient();
        using var anonymousDelete = await anonymous.DeleteAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousDelete.StatusCode);

        using var admin = await CreateSignedInClientAsync($"admin-{Guid.NewGuid():N}@example.test", makeAdmin: true);
        var before = await staff.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=100");
        Assert.Contains(before!.Items, s => s.Id == skill.Id);

        using var adminProject = await admin.DeleteAsync($"/api/projects/{project.Id}");
        using var adminSkill = await admin.DeleteAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.NoContent, adminProject.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, adminSkill.StatusCode);

        // The cached list must not keep showing the deleted record.
        var after = await staff.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?page=1&pageSize=100");
        Assert.DoesNotContain(after!.Items, s => s.Id == skill.Id);
        using var gone = await admin.GetAsync($"/api/skills/{skill.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        using var again = await admin.DeleteAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Only_admins_can_update_projects_and_skills()
    {
        using var staff = await CreateSignedInClientAsync();
        var skill = await AddSkillAsync(staff, "Original");
        using var projectResponse = await staff.PostAsJsonAsync("/api/projects", new
        {
            Title = "Original",
            Description = "Original",
            ImageUrl = "https://example.test/o.png"
        });
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectDto>())!;

        var projectUpdate = new { Title = "Edited", Description = "Edited", ImageUrl = "https://example.test/e.png" };
        var skillUpdate = new { Name = "Edited", SkillLevelId = 4 };

        using var staffProject = await staff.PutAsJsonAsync($"/api/projects/{project.Id}", projectUpdate);
        using var staffSkill = await staff.PutAsJsonAsync($"/api/skills/{skill.Id}", skillUpdate);
        Assert.Equal(HttpStatusCode.Forbidden, staffProject.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staffSkill.StatusCode);

        var email = $"admin-{Guid.NewGuid():N}@example.test";
        using var admin = await CreateSignedInClientAsync(email, makeAdmin: true);

        var before = await admin.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?all=true&page=1&pageSize=100");
        using var adminProject = await admin.PutAsJsonAsync($"/api/projects/{project.Id}", projectUpdate);
        using var adminSkill = await admin.PutAsJsonAsync($"/api/skills/{skill.Id}", skillUpdate);
        Assert.Equal(HttpStatusCode.OK, adminProject.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminSkill.StatusCode);
        Assert.Equal("Edited", (await adminProject.Content.ReadFromJsonAsync<ProjectDto>())!.Title);

        var fetched = await admin.GetFromJsonAsync<SkillDto>($"/api/skills/{skill.Id}");
        Assert.Equal(("Edited", "Expert"), (fetched!.Name, fetched.Level));
        var cached = await admin.GetFromJsonAsync<PagedResult<SkillDto>>("/api/skills?all=true&page=1&pageSize=100");
        Assert.Contains(cached!.Items, s => s.Id == skill.Id && s.Name == "Edited");
        Assert.DoesNotContain(before!.Items, s => s.Id == skill.Id && s.Name == "Edited");

        using var missing = await admin.PutAsJsonAsync("/api/projects/999999", projectUpdate);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var invalid = await admin.PutAsJsonAsync($"/api/skills/{skill.Id}", new { Name = "", SkillLevelId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Skill_levels_are_a_lookup_and_unknown_levels_are_rejected()
    {
        using var client = await CreateSignedInClientAsync();

        var levels = await client.GetFromJsonAsync<List<SkillLevelDto>>("/api/skilllevels");
        Assert.Equal(["Beginner", "Intermediate", "Advanced", "Expert"], levels!.Select(l => l.Name).ToArray());

        using var bad = await client.PostAsJsonAsync("/api/skills", new { Name = "X", SkillLevelId = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var skill = await AddSkillAsync(client, "Lookup");
        Assert.Equal("Advanced", skill.Level);
    }

    private static async Task<SkillDto> AddSkillAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/skills", new { Name = name, SkillLevelId = 3 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SkillDto>())!;
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private async Task<HttpClient> CreateSignedInClientAsync(string? email = null, bool makeAdmin = false)
    {
        var client = CreateClient();
        email ??= $"owner-{Guid.NewGuid():N}@example.test";
        const string password = "VeryStrong!Password123";
        using var response = await client.PostAsJsonAsync("/auth/register", new { Email = email, Password = password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (makeAdmin)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roles.RoleExistsAsync(Roles.Admin))
                {
                    await roles.CreateAsync(new IdentityRole(Roles.Admin));
                }

                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByEmailAsync(email);
                await users.AddToRoleAsync(user!, Roles.Admin);
            }

            using var login = await client.PostAsJsonAsync("/auth/login", new { Email = email, Password = password });
            Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
            auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
