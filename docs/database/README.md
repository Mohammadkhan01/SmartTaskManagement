# Database Setup

## Development database

The application currently uses an existing Azure SQL Edge container:

- Container: azuresqledge
- Host: localhost
- Port: 1433
- SQL login: sa

This container is managed independently of this repository.
The Docker Compose file intentionally does not create another database.

## Local environment file

Create a local settings file if needed:

    cp docker/.env.example docker/.env

Set MSSQL_SA_PASSWORD to the password configured on the existing database.
Never commit docker/.env or share its contents.

ASP.NET Core does not automatically load this Docker environment file.
The application's connection string is configured separately through
ASP.NET Core configuration and User Secrets.

## API connection string and JWT secrets

The Docker `.env` file above is not an ASP.NET Core configuration source.
Configure API settings separately with .NET User Secrets for local
development.

The API project has a `UserSecretsId`. From the repository root, use:

    dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<local SQL Server connection string>" --project src/SmartTask.API/SmartTask.API.csproj
    dotnet user-secrets set "Jwt:Issuer" "<local JWT issuer>" --project src/SmartTask.API/SmartTask.API.csproj
    dotnet user-secrets set "Jwt:Audience" "<local JWT audience>" --project src/SmartTask.API/SmartTask.API.csproj
    dotnet user-secrets set "Jwt:SigningKey" "<cryptographically random key of at least 32 UTF-8 bytes>" --project src/SmartTask.API/SmartTask.API.csproj

Replace placeholders locally; never commit actual values or paste them into
shared logs. Shell history may retain literal command-line arguments.

For production, configure these environment variables using the hosting
platform's protected configuration or secret manager:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`

Do not put production values in `appsettings.json`, `.env.example`, or source
control. The API validates required database and JWT settings during startup.

## Verify the database

    docker ps --filter "name=^/azuresqledge$"

Confirm that the container is running and port 1433 is published.

## EF Core migrations

List migrations:

    dotnet ef migrations list --project src/SmartTask.DAL/SmartTask.DAL.csproj --startup-project src/SmartTask.API/SmartTask.API.csproj

Apply existing migrations when intentionally updating the configured database:

    dotnet ef database update --project src/SmartTask.DAL/SmartTask.DAL.csproj --startup-project src/SmartTask.API/SmartTask.API.csproj

Do not create migrations merely to test database connectivity.

## Integration tests

Integration tests must use a separate test database.
Never point integration tests at production data.
