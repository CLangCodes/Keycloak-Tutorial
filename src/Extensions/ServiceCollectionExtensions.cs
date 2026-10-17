using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace Keycloak.Extensions;

internal static class ServiceCollectionExtensions
{
    private const string SecuritySchemeName = "Keycloak";

    public static IServiceCollection AddOpenApiWithAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SecuritySchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri(configuration["Keycloak:AuthorizationUrl"]!),
                            Scopes = new Dictionary<string, string>
                            {
                                { "openid", "openid" },
                                { "profile", "profile" }
                            }
                        }
                    }
                };

                return Task.CompletedTask;
            });

            // Replaces Swashbuckle's AuthorizeCheckOperationFilter: only endpoints that
            // require authorization get the Keycloak security requirement.
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var requiresAuth = metadata.OfType<IAuthorizeData>().Any()
                    && !metadata.OfType<IAllowAnonymous>().Any();

                if (requiresAuth)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SecuritySchemeName, context.Document)] = ["openid", "profile"]
                    });

                    operation.Responses ??= new OpenApiResponses();
                    operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
                    operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
