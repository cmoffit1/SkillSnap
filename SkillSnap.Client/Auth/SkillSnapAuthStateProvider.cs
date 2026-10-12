using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace SkillSnap.Client.Auth;

public sealed class SkillSnapAuthStateProvider(
    HttpClient httpClient) : AuthenticationStateProvider
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private AuthResponse? _session;
    private bool _initialized;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await EnsureInitializedAsync();
        return CreateAuthenticationState();
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync("auth/register", request, cancellationToken);
    }

    public async Task LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync("auth/login", request, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateCredentialedRequest(HttpMethod.Post, "auth/logout");
        request.Headers.Add("X-SkillSnap-Refresh", "1");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _session = null;
        _initialized = true;
        NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthenticationState()));
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (_session is null)
        {
            return null;
        }

        if (_session.ExpiresUtc <= DateTime.UtcNow.AddSeconds(30))
        {
            await RefreshSessionAsync(cancellationToken);
        }

        return _session?.AccessToken;
    }

    private async Task AuthenticateAsync<TRequest>(
        string endpoint,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateCredentialedRequest(HttpMethod.Post, endpoint);
        request.Content = JsonContent.Create(payload);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The authentication endpoint returned an empty response.");

        SetSession(session);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await RefreshSessionAsync(cancellationToken);
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task RefreshSessionAsync(CancellationToken cancellationToken)
    {
        using var request = CreateCredentialedRequest(HttpMethod.Post, "auth/refresh");
        request.Headers.Add("X-SkillSnap-Refresh", "1");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _session = null;
            return;
        }

        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<AuthResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("The refresh endpoint returned an empty response.");

        SetSession(session);
    }

    private void SetSession(AuthResponse session)
    {
        _session = session;
        _initialized = true;
        NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthenticationState()));
    }

    private AuthenticationState CreateAuthenticationState()
    {
        if (_session is null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _session.UserId),
            new(ClaimTypes.Name, _session.Email),
            new(ClaimTypes.Email, _session.Email)
        };
        claims.AddRange(_session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, "Bearer", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private static HttpRequestMessage CreateCredentialedRequest(HttpMethod method, string endpoint)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }
}
