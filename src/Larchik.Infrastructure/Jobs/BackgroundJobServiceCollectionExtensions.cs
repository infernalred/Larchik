using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Larchik.Infrastructure.Jobs;

public static class BackgroundJobServiceCollectionExtensions
{
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BackgroundJobsOptions>(configuration.GetSection("BackgroundJobs"));

        services.AddScoped<IBackgroundJobHandler, FxCbrDailyJobHandler>();
        services.AddSingleton<IJobRunPlanner, FxCbrDailyRunPlanner>();

        services.AddHostedService<BackgroundJobSchedulerService>();
        services.AddHostedService<BackgroundJobExecutorService>();

        return services;
    }
}
