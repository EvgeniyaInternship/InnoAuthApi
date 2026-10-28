using AuthApi.Api.Constants.Keycloak;
using AuthApi.Api.Interfaces;
using AuthApi.Api.Middlewares;
using AuthApi.Api.Options;
using AuthApi.Api.Services;

namespace AuthApi.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        return services;
    }

    public static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    public static IServiceCollection AddAuthServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KeycloakOptions>(
            configuration.GetSection(KeycloakSettings.SectionName));

        services.AddHttpClient<IAuthService, AuthService>();

        return services;
    }
}
