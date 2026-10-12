using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SkillSnap.Api.Contracts.Auth;

namespace SkillSnap.Api.Tests;

public sealed class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Api_origin_serves_the_Blazor_client()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>SkillSnap.Client</title>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_creates_staff_account_and_returns_access_token()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            Email = "staff@example.test",
            Password = "VeryStrong!Password123"
        });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var refreshCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.True(refreshCookie.Contains("SameSite=Lax", StringComparison.OrdinalIgnoreCase));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Contains("Staff", auth.Roles);
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var currentUserResponse = await client.GetAsync("/auth/me");
        Assert.True(
            currentUserResponse.IsSuccessStatusCode,
            $"{currentUserResponse.StatusCode} {currentUserResponse.Headers.WwwAuthenticate} " +
            await currentUserResponse.Content.ReadAsStringAsync());
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(currentUser);
        Assert.Equal("staff@example.test", currentUser.Email);
    }

    [Fact]
    public async Task Reusing_rotated_refresh_token_revokes_the_session_family()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            Email = "refresh@example.test",
            Password = "VeryStrong!Password123"
        });
        Assert.True(registerResponse.IsSuccessStatusCode, await registerResponse.Content.ReadAsStringAsync());
        var originalToken = ReadRefreshCookie(registerResponse);

        var rotationResponse = await SendRefreshAsync(client, originalToken);
        Assert.True(rotationResponse.IsSuccessStatusCode, await rotationResponse.Content.ReadAsStringAsync());
        var rotatedToken = ReadRefreshCookie(rotationResponse);

        var reuseResponse = await SendRefreshAsync(client, originalToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        var afterReuseResponse = await SendRefreshAsync(client, rotatedToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuseResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        request.Headers.Add("X-SkillSnap-Refresh", "1");
        request.Headers.Add("Cookie", $"__Secure-SkillSnapRefresh={token}");
        return await client.SendAsync(request);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        const string prefix = "__Secure-SkillSnapRefresh=";
        var start = cookie.IndexOf(prefix, StringComparison.Ordinal);
        Assert.NotEqual(-1, start);
        start += prefix.Length;
        var end = cookie.IndexOf(';', start);
        return end < 0 ? cookie[start..] : cookie[start..end];
    }
}
