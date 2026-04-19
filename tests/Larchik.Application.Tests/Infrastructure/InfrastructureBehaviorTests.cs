using System.Security.Claims;
using Larchik.Infrastructure.Jobs;
using Larchik.Infrastructure.Security;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Larchik.Application.Tests.Infrastructure;

public sealed class InfrastructureBehaviorTests
{
    [Fact]
    public void JobScheduleCalculator_UsesConfiguredDailyTime_OrNextDayIfAlreadyPassed()
    {
        var definition = new JobDefinition
        {
            ScheduleType = JobScheduleType.DailyUtc,
            ScheduleValue = "05:10"
        };

        var sameDay = JobScheduleCalculator.ComputeNextRunUtc(
            definition,
            new DateTime(2026, 4, 20, 5, 0, 0, DateTimeKind.Utc));
        var nextDay = JobScheduleCalculator.ComputeNextRunUtc(
            definition,
            new DateTime(2026, 4, 20, 5, 10, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 4, 20, 5, 10, 0, DateTimeKind.Utc), sameDay);
        Assert.Equal(new DateTime(2026, 4, 21, 5, 10, 0, DateTimeKind.Utc), nextDay);
    }

    [Fact]
    public void JobScheduleCalculator_FallsBack_ForInvalidScheduleValues()
    {
        var daily = new JobDefinition
        {
            ScheduleType = JobScheduleType.DailyUtc,
            ScheduleValue = "broken"
        };
        var interval = new JobDefinition
        {
            ScheduleType = JobScheduleType.IntervalMinutes,
            ScheduleValue = "0"
        };
        var unknown = new JobDefinition
        {
            ScheduleType = (JobScheduleType)999,
            ScheduleValue = "ignored"
        };
        var now = new DateTime(2026, 4, 20, 4, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 4, 20, 5, 10, 0, DateTimeKind.Utc), JobScheduleCalculator.ComputeNextRunUtc(daily, now));
        Assert.Equal(now.AddMinutes(60), JobScheduleCalculator.ComputeNextRunUtc(interval, now));
        Assert.Equal(now.AddMinutes(5), JobScheduleCalculator.ComputeNextRunUtc(unknown, now));
    }

    [Fact]
    public void RunPlanners_ProduceExpectedDedupKeys()
    {
        var definition = new JobDefinition();
        var now = new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Utc);

        var fxRuns = new FxCbrDailyRunPlanner().BuildRuns(definition, now).ToArray();
        var moexRuns = new MoexPricesDailyRunPlanner().BuildRuns(definition, now).ToArray();
        var tbankRuns = new TbankPricesDailyRunPlanner().BuildRuns(definition, now).ToArray();
        var instrumentInfoRuns = new TbankInstrumentInfoDailyRunPlanner().BuildRuns(definition, now).ToArray();

        Assert.Equal(["fx:cbr:2026-04-20", "fx:cbr:2026-04-19"], fxRuns.Select(x => x.DedupKey));
        Assert.Equal(["prices:moex:2026-04-20", "prices:moex:2026-04-19"], moexRuns.Select(x => x.DedupKey));
        Assert.Equal(["prices:tbank:2026-04-20", "prices:tbank:2026-04-19"], tbankRuns.Select(x => x.DedupKey));
        Assert.Single(instrumentInfoRuns);
        Assert.Equal("instrument-info:tbank:2026-04-20", instrumentInfoRuns[0].DedupKey);
    }

    [Fact]
    public void UserAccessor_ReadsNameIdentifierClaim()
    {
        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ]))
        };
        var accessor = new UserAccessor(new HttpContextAccessor { HttpContext = httpContext });

        var actual = accessor.GetUserId();

        Assert.Equal(userId, actual);
    }

    [Fact]
    public void UserAccessor_ThrowsClearError_WhenClaimIsMissing()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        var accessor = new UserAccessor(new HttpContextAccessor { HttpContext = httpContext });

        var error = Assert.Throws<InvalidOperationException>(() => accessor.GetUserId());

        Assert.Equal("Authenticated user id claim is missing.", error.Message);
    }
}
