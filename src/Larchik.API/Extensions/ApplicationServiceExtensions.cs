using Larchik.API.Configuration;
using Larchik.API.DTOs;
using Larchik.Application.Currencies.GetCurrencies;
using Larchik.Application.Contracts;
using Larchik.Persistence.Context;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Larchik.Infrastructure.Recalculation;
using Larchik.Application.Operations.ImportBroker;
using System.Text.Json.Serialization;

namespace Larchik.API.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var importOptions = configuration.GetSection(ImportOptions.SectionName).Get<ImportOptions>() ?? new ImportOptions();

        services.AddDbContext<LarchikContext>(opt =>
        {
            opt.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            opt.UseSnakeCaseNamingConvention();
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetCurrenciesQuery).Assembly));
        services.AddValidatorsFromAssemblyContaining<GetCurrenciesQuery>();
        services.AddValidatorsFromAssemblyContaining<ImportBrokerReportRequestValidator>();
        services.AddFluentValidationAutoValidation();

        services.AddControllers(opt =>
        {
            var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            opt.Filters.Add(new AuthorizeFilter(policy));
        })
        .AddJsonOptions(opt =>
        {
            opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddHttpContextAccessor();
        services.AddScoped<IPortfolioRecalcService, PortfolioRecalcService>();
        services.AddSingleton<IBrokerReportParser, TbankReportParser>();
        services.Configure<ImportOptions>(configuration.GetSection(ImportOptions.SectionName));
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = importOptions.MaxFileSizeBytes;
        });

        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        return services;
    }
}
