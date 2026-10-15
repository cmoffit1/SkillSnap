using System.Net.Http.Headers;
using System.Net.Http.Json;
using SkillSnap.Client.Auth;

namespace SkillSnap.Client.Services;

public abstract class ApiClientBase(HttpClient httpClient, SkillSnapAuthStateProvider auth)
{
    protected async Task<T?> GetAsync<T>(string uri, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, uri, null, ct);
        return await ReadAsync<T>(response, ct);
    }

    protected async Task<TResponse> PostAsync<TRequest, TResponse>(
        string uri, TRequest payload, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Post, uri, JsonContent.Create(payload), ct);
        return await ReadAsync<TResponse>(response, ct)
            ?? throw new ApiException("The server returned an empty response.");
    }

    protected async Task<TResponse> PutAsync<TRequest, TResponse>(
        string uri, TRequest payload, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Put, uri, JsonContent.Create(payload), ct);
        return await ReadAsync<TResponse>(response, ct)
            ?? throw new ApiException("The server returned an empty response.");
    }

    protected async Task DeleteAsync(string uri, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, uri, null, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string uri, HttpContent? content, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(method, uri, ct);
        request.Content = content;

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("The server took too long to respond. Please try again.", inner: ex);
        }
        catch (HttpRequestException ex) when (ex is not ApiException)
        {
            throw new ApiException("Can't reach the server. Check your connection and try again.", inner: ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                throw await ApiException.FromResponseAsync(response, ct);
            }
        }

        return response;
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ApiException("The server sent a response we couldn't read.", inner: ex);
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method, string uri, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("api-version", "1.0");
        var token = await auth.GetAccessTokenAsync(ct);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }
}
