using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using SkillSnap.Client.Auth;
using SkillSnap.Client;
using SkillSnap.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var appUri = new Uri(builder.HostEnvironment.BaseAddress);
var configuredApiBaseAddress = builder.Configuration["ApiBaseAddress"];
var apiBaseAddress = string.IsNullOrWhiteSpace(configuredApiBaseAddress)
    ? builder.HostEnvironment.BaseAddress
    : configuredApiBaseAddress;
var isDevelopment = builder.HostEnvironment.IsDevelopment();

if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out var apiUri) ||
    !isDevelopment && apiUri.Scheme != Uri.UriSchemeHttps ||
    !isDevelopment &&
    !string.Equals(
        apiUri.GetLeftPart(UriPartial.Authority),
        appUri.GetLeftPart(UriPartial.Authority),
        StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "ApiBaseAddress must use HTTPS and match the app origin outside Development.");
}

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = apiUri, Timeout = TimeSpan.FromSeconds(15) });
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<SkillSnapAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    services => services.GetRequiredService<SkillSnapAuthStateProvider>());

builder.Services.AddScoped<UserState>();

builder.Services.AddScoped<SkillService>();
builder.Services.AddScoped<ProjectService>();

await builder.Build().RunAsync();
