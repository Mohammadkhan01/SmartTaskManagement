# Smart Task Management System

A requirements-driven task and project management application built with ASP.NET Core, Entity Framework Core, SQL Server and Angular.

## Architecture

- Frontend: Angular standalone application, deployed to Vercel.
- Backend: ASP.NET Core Web API using N-Layer organization.
- Database: SQL Server, run locally in Docker during development.
- Source control: GitHub.

Production API and SQL Server hosting are configured separately from the Angular Vercel deployment unless a suitable container deployment is deliberately selected and verified.

## Repository layout

```text
src/       Backend projects
frontend/  Angular application
tests/     Automated tests
docker/    Local Docker Compose configuration
docs/      Architecture and database documentation
scripts/   Development helper scripts
```

## Prerequisites

- Git
- .NET 10 SDK (or the exact SDK version agreed for the assignment)
- Node.js LTS and npm
- Angular CLI compatible with the selected Angular version
- Docker Desktop

## Start local SQL Server

```bash
cp docker/.env.example docker/.env
# Edit docker/.env and replace the sample password with a strong local password.
docker compose --env-file docker/.env -f docker/docker-compose.yml up -d
```

Check container status:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml ps
```

Stop SQL Server without deleting data:

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml down
```

Delete the local database volume (DESTRUCTIVE; removes local SQL Server data):

```bash
docker compose --env-file docker/.env -f docker/docker-compose.yml down -v
```

## API configuration and secrets

The API loads local development secrets through .NET User Secrets. The API
project already has a `UserSecretsId`, and `WebApplication.CreateBuilder(args)`
loads User Secrets when the application runs in the Development environment.

Configure these keys for the API project:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:SigningKey`

Use `dotnet user-secrets set` with the API project path to configure local
values. Do not put real values in source files, scripts, committed `.env`
files, or shared terminal transcripts. Be aware that literal command-line
arguments may be retained in shell history.

The JWT signing key must contain at least 32 UTF-8 bytes. Use a
cryptographically random key; do not reuse a password or commit the key.

For production, supply the following environment variables through the
hosting platform's protected configuration or secret manager:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`

ASP.NET Core maps the double underscore in environment variable names to
configuration section separators. Do not store production credentials in
`appsettings.json`, Docker environment examples, or source control.

The Docker `docker/.env` file configures local Docker tooling only. It is not
automatically loaded by the ASP.NET Core API. The API database connection
string must be configured separately.

The API fails startup when required database or JWT configuration is missing
or invalid. Never print secret values when diagnosing configuration.

## Production API security and rate limiting

The API applies configurable per-client-IP rate limiting to authentication
registration, login and refresh endpoints. The current default is 10 requests
per 60 seconds, with excess requests receiving HTTP 429.

When the API is deployed behind a reverse proxy or load balancer:

- Configure ASP.NET Core Forwarded Headers Middleware to trust only the
  explicitly configured proxy addresses or networks.
- Configure the proxy to remove or replace untrusted incoming forwarded
  headers before forwarding requests.
- Run Forwarded Headers Middleware before routing and rate-limiting middleware.
- Never trust arbitrary client-supplied `X-Forwarded-For` values.

Without trusted forwarded-header configuration, the API uses the direct
connection's remote IP address. Behind a proxy, this may be the proxy's IP
rather than the original client IP, causing clients to share a rate-limit
partition.

Do not enable forwarded headers with unrestricted proxy trust.

## Next implementation step

Create the .NET solution and N-Layer projects, then configure ASP.NET Core Identity, EF Core and the first database migration. Do not create production credentials in this repository.
