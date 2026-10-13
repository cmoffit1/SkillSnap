# Database and authentication setup

## Local development

The API uses SQL Server for both its local database and Azure SQL. Local secrets are stored with .NET User Secrets, not in `appsettings.json`.

Both projects have HTTPS-only local launch profiles. Trust the ASP.NET Core development certificate on a new machine before running the solution:

```powershell
dotnet dev-certs https --trust
```

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=SkillSnap;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project .\SkillSnap.Api\SkillSnap.Api.csproj

$keyBytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($keyBytes)
dotnet user-secrets set "Jwt:Key" ([Convert]::ToBase64String($keyBytes)) --project .\SkillSnap.Api\SkillSnap.Api.csproj

dotnet tool restore
dotnet tool run dotnet-ef database update --project .\SkillSnap.Api\SkillSnap.Api.csproj --startup-project .\SkillSnap.Api\SkillSnap.Api.csproj
```

To seed a local administrator, set both values in User Secrets. The password must satisfy the API's Identity password policy.

```powershell
dotnet user-secrets set "Seed:AdminEmail" "your-admin-email" --project .\SkillSnap.Api\SkillSnap.Api.csproj
dotnet user-secrets set "Seed:AdminPassword" "your-strong-password" --project .\SkillSnap.Api\SkillSnap.Api.csproj
```

The API does not apply migrations automatically. Apply the migration before starting the API; startup then creates the `Admin`, `Manager`, and `Staff` roles and, when configured, the initial administrator. Public registration assigns only the `Staff` role.

## Azure configuration

The API App Service also serves the Blazor WebAssembly client's published static files. The client and API therefore share one HTTPS origin in Azure. Publishing the API project builds and packages the referenced client; no separate Static Web App is required.

Publish from the repository root with:

```powershell
dotnet publish .\SkillSnap.Api\SkillSnap.Api.csproj --configuration Release
```

For the App Service:

1. Enable its system-assigned managed identity.
2. Grant that identity **Key Vault Secrets User** on the vault.
3. Set the nonsecret App Service setting `KeyVault__Uri` to the vault's HTTPS URI.
4. Add these Key Vault secrets:
   - `ConnectionStrings--DefaultConnection`
   - `Jwt--Key`
   - `Seed--AdminEmail`
   - `Seed--AdminPassword`
5. Grant the API managed identity access to the Azure SQL database. Use a connection string with Microsoft Entra managed identity authentication, for example:

   ```text
   Server=tcp:<server>.database.windows.net,1433;Database=SkillSnap;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
   ```

6. Do not set `ApiBaseAddress` in production. The client defaults to the same HTTPS origin as the API. The separate client development server uses `https://localhost:7250`, configured in `SkillSnap.Client\wwwroot\appsettings.Development.json`.
7. Use an App Service plan that supports the selected application stack. The Free F1 tier doesn't support custom domains, but the built-in `*.azurewebsites.net` hostname is sufficient for the same-origin deployment. Free-tier quotas and availability limits still apply.
8. Enable **HTTPS Only** in the App Service configuration so Azure rejects plain-HTTP client requests. The API also emits HSTS outside Development and redirects HTTP requests to HTTPS.

In Development, `Cors:AllowedOrigins` is required because the client and API use separate local HTTPS ports. Production doesn't need CORS when the client is served by the API from the same origin. The refresh cookie is `HttpOnly`, `Secure`, and `SameSite=Lax`; refresh/logout also require the `X-SkillSnap-Refresh` header, and the client sends credentials only for authentication requests.

Apply database migrations before deploying the API. Keep migration execution in the deployment pipeline or run the EF command manually; do not enable automatic migrations at application startup. The production API requires the initial administrator email and password to be present in Key Vault.

Azure SQL Database's current free offer provides up to 100,000 vCore-seconds and 32 GB each of data and backup per database per month. Choose the option to auto-pause at the free limit to avoid continuing into billable usage; a paused database is unavailable until its free allowance renews. The offer has eligibility and service limits, and Azure Key Vault, custom domains, and other resources can still incur costs.

## Authentication endpoints

- `POST /auth/register` creates an account with the `Staff` role.
- `POST /auth/login` authenticates an account.
- `POST /auth/refresh` rotates the refresh token and returns a new access token.
- `POST /auth/logout` revokes the current refresh token.
- `GET /auth/me` returns the authenticated user's identity and roles.

Access tokens expire after 15 minutes by default. Refresh tokens are opaque, stored only as hashes in SQL, and sent to the browser in an `HttpOnly`, `Secure` cookie.

## References

- [Use Azure Key Vault configuration provider in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/key-vault-configuration?view=aspnetcore-10.0)
- [Host and deploy ASP.NET Core Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0)
- [Set up an existing custom domain in Azure App Service](https://learn.microsoft.com/azure/app-service/app-service-web-tutorial-custom-domain)
- [Azure SQL Database free offer](https://learn.microsoft.com/azure/azure-sql/database/free-offer?view=azuresql)
- [Azure App Service managed identities](https://learn.microsoft.com/azure/app-service/overview-managed-identity)
