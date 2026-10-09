using EventExamProject.Application;
using EventExamProject.Infrastructure;

namespace EventExamProject.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();

        services.AddApplicationServices();
        services.AddInfrastructureServices(configuration);

        return services;
    }
}
