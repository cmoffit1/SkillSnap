# Deploying SkillSnap to Azure (free tier)

One App Service hosts the API **and** the Blazor client (same origin, HTTPS only).

| Resource | SKU | Cost |
|---|---|---|
| App Service plan + web app | F1 Free, Linux | Free (60 CPU min/day, no Always On, so cold starts) |
| Azure SQL Database | Free offer (serverless, 100k vCore-s/month, auto-pause) | Free; one per subscription |
| Key Vault | Standard | Cents per month |
| Application Insights + Log Analytics | 1 GB/day cap | Free for the first 5 GB/month |
| GitHub Actions | Public repo, or 2,000 min/month private | Free |

## Workflows (`.github\workflows`)

- `ci.yml`: build, test and validate Bicep on every PR and push to `master`.
- `infra.yml`: **manual**. Creates/updates the resources in `infra\main.bicep` and writes the app secrets to Key Vault.
- `deploy.yml`: on push to `master` (after CI): publish, apply EF migrations with a bundle, deploy, smoke test.
- `codeql.yml` and `dependabot.yml`: security scanning and dependency updates.

## One-time setup

1. **Resource group and identity.** In Azure, create an app registration with a federated credential for
   `repo:cmoffit1/SkillSnap:environment:production` (GitHub OIDC), and give it **Contributor** and
   **User Access Administrator** on the resource group (the Bicep creates a role assignment for the web app).
   The resource group must exist first: `az group create -n rg-skillsnap -l <allowed region>`.
2. **GitHub environment** `production` (Settings, Environments). Optionally require a reviewer.
3. **Repository variables** (Settings, Secrets and variables, Actions, Variables):
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`,
   `SEED_ADMIN_EMAIL`, and optionally `SQL_ADMIN_LOGIN` (default `skillsnapadmin`).
4. **Repository secrets**: `SQL_ADMIN_PASSWORD`, `JWT_KEY` (32+ chars), `SEED_ADMIN_PASSWORD`.
5. Run **Infrastructure** from the Actions tab, picking a region your student subscription allows.
6. Push to `master` (or run **Deploy**). The site URL is shown on the deployment.

The initial admin (`Seed--AdminEmail`) is created at startup. Self-registered users are Staff.

## Notes

- Secrets never live in the repo or the app settings; the web app reads them from Key Vault with its managed identity
  (`KeyVault__Uri`). Key Vault names: `ConnectionStrings--DefaultConnection`, `Jwt--Key`, `Seed--AdminEmail`, `Seed--AdminPassword`.
- Migrations run from the pipeline; the app does not migrate at startup. The runner's IP is added to the SQL firewall
  only for the migration step, then removed.
- The free database pauses when idle, so the first request after a pause can take about a minute.
- `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, but telemetry needs the Azure Monitor OpenTelemetry package
  (`Azure.Monitor.OpenTelemetry.AspNetCore`) added to the API; add alerts in the portal once it is.
- If the free SQL offer is unavailable in your region or subscription, change the `database` resource to the `Basic` SKU (about $5/month).
