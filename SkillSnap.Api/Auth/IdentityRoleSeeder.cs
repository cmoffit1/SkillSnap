using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SkillSnap.Api.Auth;

public sealed class IdentityRoleSeeder(
    RoleManager<IdentityRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<IdentityRoleSeeder> logger)
{
    public async Task SeedAsync()
    {
        foreach (var roleName in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create the Identity role '{roleName}': " +
                    string.Join("; ", result.Errors.Select(error => error.Description)));
            }
        }

        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Seed:AdminEmail and Seed:AdminPassword must be configured in production.");
            }

            logger.LogInformation("No initial administrator configured; skipping administrator seeding.");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Seed:AdminEmail and Seed:AdminPassword must either both be set or both be absent.");
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the initial administrator: " +
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            var addRoleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not assign the Admin role to the initial administrator: " +
                    string.Join("; ", addRoleResult.Errors.Select(error => error.Description)));
            }
        }
    }
}
