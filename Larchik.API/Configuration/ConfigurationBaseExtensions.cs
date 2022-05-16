﻿using System.Net.Http.Headers;
using FluentValidation.AspNetCore;
using Larchik.API.Configuration.Models;
using Larchik.Application.Contracts;
using Larchik.Application.Deals;
using Larchik.Application.Helpers;
using Larchik.Application.Services;
using Larchik.Infrastructure.Market;
using Larchik.Infrastructure.Security;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Larchik.API.Configuration;

public static class ConfigurationBaseExtensions
{
    public static IServiceCollection AddBaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DataContext>(opt =>
            {
                opt.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            });

            services.AddAutoMapper(typeof(MappingProfiles).Assembly);
            services.AddMediatR(typeof(Create).Assembly);
            services.AddControllers(opt =>
            {
                var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                opt.Filters.Add(new AuthorizeFilter(policy));
            })
            .AddFluentValidation(cfg => 
            {
                cfg.RegisterValidatorsFromAssemblyContaining<Create>();
            });

            var marketConfig = configuration.GetSection(nameof(MarketSettings)).Get<MarketSettings>();
            
            services.AddHttpClient("Market", client =>
            {
                client.BaseAddress = new Uri(marketConfig.BaseAddress);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", marketConfig.Token);
            });

            services.AddScoped<IUserAccessor, UserAccessor>();
            services.AddScoped<IDealService, DealService>();
            services.AddSingleton<IMarketAccessor, MarketAccessor>();
            services.AddScoped<LastPriceUpdater>();
            services.AddHostedService<LastPriceWorker>();
            services.Configure<RouteOptions>(options => options.LowercaseUrls = true);
            
        return services;
    }
}