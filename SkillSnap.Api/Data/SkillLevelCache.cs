using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkillSnap.Api.Models;

namespace SkillSnap.Api.Data;

/// <summary>The skill level lookup, held in memory for 24 hours and loaded at startup.</summary>
public class SkillLevelCache(IMemoryCache cache)
{
    private const string Key = "skill-levels";

    public async Task<IReadOnlyList<SkillLevelDto>> GetAsync(SkillSnapContext db, CancellationToken ct) =>
        (await cache.GetOrCreateAsync(Key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return (IReadOnlyList<SkillLevelDto>)await db.SkillLevels
                .AsNoTracking()
                .OrderBy(level => level.SortOrder)
                .Select(level => new SkillLevelDto(level.Id, level.Name))
                .ToListAsync(ct);
        }))!;

    /// <summary>Drops the cached list so the next read reloads it; call after levels change.</summary>
    public void Invalidate() => cache.Remove(Key);
}

/// <summary>Loads the skill levels when the app starts. A failure is logged and the first request loads them instead.</summary>
public class SkillLevelWarmup(
    IServiceScopeFactory scopes, SkillLevelCache levels, ILogger<SkillLevelWarmup> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SkillSnapContext>();
            var loaded = await levels.GetAsync(db, cancellationToken);
            logger.LogInformation("Loaded {Count} skill levels into memory.", loaded.Count);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not preload skill levels; they will load on first use.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
