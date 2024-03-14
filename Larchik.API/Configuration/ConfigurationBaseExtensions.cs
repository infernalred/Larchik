using System.Net.Http.Headers;
using FluentValidation;
using FluentValidation.AspNetCore;
using Larchik.API.Configuration.Models;
using Larchik.Application.Contracts;
using Larchik.Application.Deals;
using Larchik.Application.Services;
using Larchik.Application.Services.Contracts;
using Larchik.Infrastructure.ExchangeServices.Cbr;
using Larchik.Infrastructure.Market;
using Larchik.Infrastructure.Security;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Larchik.API.Configuration;

public static class ConfigurationBaseExtensions
{
    public static IServiceCollection AddBaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DataContext>(opt =>
        {
            opt.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        LogManager.Configuration.Variables["DefaultConnection"] = configuration.GetConnectionString("DefaultConnection");
        services.AddMemoryCache();
        services.AddMediatR(typeof(Create).Assembly);
        services.AddControllers(opt =>
            {
                var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                opt.Filters.Add(new AuthorizeFilter(policy));
            });

        services
            .AddValidatorsFromAssemblyContaining(typeof(Create))
            .AddFluentValidationAutoValidation()
            .AddFluentValidationClientsideAdapters();

        var marketConfig = configuration.GetSection(nameof(MarketSettings)).Get<MarketSettings>();

        if (string.IsNullOrEmpty(marketConfig?.BaseAddress))
            throw new ArgumentException($"{nameof(MarketSettings.BaseAddress)} is null");

        if (string.IsNullOrEmpty(marketConfig.Token))
            throw new ArgumentException($"{nameof(MarketSettings.Token)} is null");

        services.AddHttpClient("Market", client =>
        {
            client.BaseAddress = new Uri(marketConfig.BaseAddress);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", marketConfig.Token);
        });

        services.Configure<CbrSettings>(configuration.GetSection(nameof(CbrSettings)));
        services.AddScoped<IUserAccessor, UserAccessor>();
        //services.AddScoped<IDealService, DealService>();
        services.AddSingleton<IMarketAccessor, MarketAccessor>();

        //services.AddScoped<LastPriceUpdater>();
        services.AddScoped<CbrExchangeRates>();
        //services.AddHostedService<LastPriceWorker>();
        services.AddHostedService<CbrExchangeWorker>();

        services.AddScoped<IExchangeService, ExchangeService>();
        //services.AddScoped<IPortfolioService, PortfolioService>();

        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        return services;
    }
}