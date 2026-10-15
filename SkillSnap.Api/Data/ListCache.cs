using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace SkillSnap.Api.Data;

public static class CacheSets
{
    public const string Projects = "projects";
    public const string Skills = "skills";
}

/// <summary>
/// In-memory cache for paged list responses. Each set has a version that is part of the key,
/// so invalidating a set makes all of its cached pages unreachable; they expire on their own.
/// </summary>
public class ListCache(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<string, long> versions = new();

    public async Task<T> GetOrCreateAsync<T>(
        string set, string scope, int page, int pageSize, Func<Task<T>> factory)
    {
        var key = $"{set}:v{versions.GetValueOrDefault(set)}:{scope}:{page}:{pageSize}";
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory();
        cache.Set(key, value, Lifetime);
        return value;
    }

    public void Invalidate(string set) => versions.AddOrUpdate(set, 1, (_, version) => version + 1);

    /// <summary>
    /// Caches an account's portfolio ID so list requests don't query for it each time. Only real
    /// IDs are kept: 0 means "no portfolio yet", which changes on the account's first create.
    /// </summary>
    public async Task<int> GetOwnerIdAsync(string userId, Func<Task<int>> factory)
    {
        var key = $"owner:{userId}";
        if (cache.TryGetValue(key, out int cached))
        {
            return cached;
        }

        var value = await factory();
        if (value != 0)
        {
            cache.Set(key, value, TimeSpan.FromMinutes(10));
        }

        return value;
    }
}
