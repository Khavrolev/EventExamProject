using EventExamProject.Application;
using EventExamProject.Infrastructure;

namespace EventExamProject.Presentation.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddJwtAuthentication(configuration);

        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        return services;
    }
}
