#:package Aspire.Hosting.Keycloak@13.6.1-preview.1.26506.6
#:package Aspire.Hosting.PostgreSQL@13.6.1
#:package CommunityToolkit.Aspire.Hosting.Keycloak.Extensions@13.6.1-beta.935
#:sdk Aspire.AppHost.Sdk@13.6.1
#:property AspireUseCliBundle=true

var builder = DistributedApplication.CreateBuilder(args);

// Keycloak's backing store. Persistent + data volume so realms survive AppHost restarts.
var postgres = builder.AddPostgres("keycloak-postgres")
    .WithDataVolume()
    .WithHostPort(5432)
    .WithPgAdmin(pgAdmin => pgAdmin.WithHostPort(5050))
    .WithLifetime(ContainerLifetime.Persistent);

var keycloakDb = postgres.AddDatabase("keycloakdb");

var keycloakSchema = builder.AddContainer("keycloak-schema", "postgres", "17")
    .WithEntrypoint("psql")
    .WithArgs(keycloakDb.Resource.UriExpression, "-v", "ON_ERROR_STOP=1",
              "-c", "CREATE SCHEMA IF NOT EXISTS keycloak;")
    .WaitFor(keycloakDb);

var adminUser = builder.AddParameter("kc-admin-user", "kc_admin");
var adminPass = builder.AddParameter("kc-admin-password", secret: true);

// Stable port so browser cookies / OIDC redirect URIs stay valid across runs.
var keycloak = builder.AddKeycloak("keycloak", 8080, adminUser, adminPass)
    .WithPostgres(keycloakDb)
    .WithEnvironment("KC_DB_SCHEMA", "keycloak")
    .WaitForCompletion(keycloakSchema)
    .WithDataVolume()
    .WithRealmImport("./Realms")
    .WithOtlpExporter()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject("api", "./src/Tutorial.csproj")
    .WithReference(keycloak)
    .WaitFor(keycloak)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
