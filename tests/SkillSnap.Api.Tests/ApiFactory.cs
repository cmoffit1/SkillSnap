using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillSnap.Api.Data;

namespace SkillSnap.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly Dictionary<string, string?> _originalEnvironment = [];

    public ApiFactory()
    {
        _connection.Open();
        SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=localhost;Database=Test;");
        SetEnvironmentVariable("Jwt__Issuer", "SkillSnap.Tests");
        SetEnvironmentVariable("Jwt__Audience", "SkillSnap.Tests.Client");
        SetEnvironmentVariable("Jwt__Key", "integration-test-signing-key-that-is-at-least-32-bytes");
        SetEnvironmentVariable("Jwt__AccessTokenMinutes", "15");
        SetEnvironmentVariable("Cors__AllowedOrigins__0", "https://localhost:7292");
        SetEnvironmentVariable("RateLimit__AuthPermitLimit", "1000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<SkillSnapContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SkillSnapContext>>();
            var contextPoolRegistrations = services
                .Where(descriptor =>
                    descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.GetGenericArguments().Contains(typeof(SkillSnapContext)) &&
                    descriptor.ServiceType.Namespace == "Microsoft.EntityFrameworkCore.Internal")
                .ToArray();
            foreach (var descriptor in contextPoolRegistrations)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<SkillSnapContext>();
            services.RemoveAll<DbConnection>();
            services.AddSingleton<DbConnection>(_connection);
            services.AddDbContextPool<SkillSnapContext>(options => options.UseSqlite(_connection));

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<SkillSnapContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            foreach (var (name, value) in _originalEnvironment)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private void SetEnvironmentVariable(string name, string value)
    {
        _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}
