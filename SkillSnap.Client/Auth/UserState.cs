using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SkillSnap.Client.Auth;

/// <summary>The signed-in user's identity as exposed to components.</summary>
public sealed record CurrentUser(string UserId, string Email, IReadOnlyList<string> Roles);

/// <summary>
/// Holds the signed-in user's info for the app. It follows the authentication state, so components
/// can read <see cref="Current"/> and subscribe to <see cref="Changed"/> instead of parsing claims.
/// </summary>
public sealed class UserState : IDisposable
{
    private readonly AuthenticationStateProvider _authProvider;

    public UserState(AuthenticationStateProvider authProvider)
    {
        _authProvider = authProvider;
        _authProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    public CurrentUser? Current { get; private set; }

    public bool IsAuthenticated => Current is not null;

    public bool IsAdmin => IsInRole("Admin");

    /// <summary>
    /// Admin-only choice to list everyone's projects and skills instead of just their own.
    /// Always false for non-admins, and reset on sign-in, sign-out or role change.
    /// </summary>
    public bool ViewAll
    {
        get => _viewAll && IsAdmin;
        set => _viewAll = value && IsAdmin;
    }

    private bool _viewAll;

    /// <summary>Raised after <see cref="Current"/> changes (sign in, sign out, or role change).</summary>
    public event Action? Changed;

    public bool IsInRole(string role) =>
        Current?.Roles.Contains(role, StringComparer.OrdinalIgnoreCase) == true;

    public async Task InitializeAsync() =>
        Update(await _authProvider.GetAuthenticationStateAsync());

    public void Dispose() => _authProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;

    private async void OnAuthenticationStateChanged(Task<AuthenticationState> task) =>
        Update(await task);

    private void Update(AuthenticationState state)
    {
        var next = FromPrincipal(state.User);
        if (next is null ? Current is null : Current is not null &&
            next.UserId == Current.UserId && next.Email == Current.Email &&
            next.Roles.SequenceEqual(Current.Roles))
        {
            return;
        }

        Current = next;
        _viewAll = false;
        Changed?.Invoke();
    }

    private static CurrentUser? FromPrincipal(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return new CurrentUser(
            principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.Identity.Name ?? string.Empty,
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray());
    }
}
