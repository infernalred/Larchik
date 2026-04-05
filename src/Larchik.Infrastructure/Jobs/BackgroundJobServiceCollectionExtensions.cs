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
        services.AddScoped<IBackgroundJobHandler, MoexPricesDailyJobHandler>();
        services.AddScoped<IBackgroundJobHandler, TbankPricesDailyJobHandler>();
        services.AddScoped<IBackgroundJobHandler, TbankInstrumentInfoDailyJobHandler>();
        services.AddSingleton<IJobRunPlanner, FxCbrDailyRunPlanner>();
        services.AddSingleton<IJobRunPlanner, MoexPricesDailyRunPlanner>();
        services.AddSingleton<IJobRunPlanner, TbankPricesDailyRunPlanner>();
        services.AddSingleton<IJobRunPlanner, TbankInstrumentInfoDailyRunPlanner>();

        services.AddHostedService<BackgroundJobSchedulerService>();
        services.AddHostedService<BackgroundJobExecutorService>();

        return services;
    }
}
