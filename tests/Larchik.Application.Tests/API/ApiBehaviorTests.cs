using System.Text;
using System.Text.Json;
using Larchik.API.Configuration;
using Larchik.API.DTOs;
using Larchik.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Larchik.Application.Tests.API;

public sealed class ApiBehaviorTests
{
    [Fact]
    public async Task ExceptionMiddleware_ReturnsDetailedPayload_InDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionMiddleware>.Instance,
            new TestHostEnvironment("Development"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var json = JsonDocument.Parse(payload);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal("boom", json.RootElement.GetProperty("message").GetString());
        Assert.True(json.RootElement.TryGetProperty("details", out _));
    }

    [Fact]
    public async Task ExceptionMiddleware_HidesDetails_OutsideDevelopment()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionMiddleware>.Instance,
            new TestHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var payload = await new StreamReader(context.Response.Body).ReadToEndAsync();
        using var json = JsonDocument.Parse(payload);

        Assert.Equal("Server Error", json.RootElement.GetProperty("message").GetString());
        Assert.True(!json.RootElement.TryGetProperty("details", out var details) || details.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
    }

    [Fact]
    public void ImportBrokerReportRequestValidator_RejectsMissingOrTooLargeFile()
    {
        var validator = new ImportBrokerReportRequestValidator(
            Options.Create(new ImportOptions
            {
                MaxFileSizeMb = 1
            }));

        var emptyResult = validator.Validate(new ImportBrokerReportRequest { File = null });
        var oversizedBytes = new byte[1_048_577];
        var largeFile = new FormFile(new MemoryStream(oversizedBytes), 0, oversizedBytes.Length, "file", "report.xlsx");
        var largeResult = validator.Validate(new ImportBrokerReportRequest { File = largeFile });

        Assert.Contains(emptyResult.Errors, x => x.PropertyName == "file");
        Assert.Contains(largeResult.Errors, x => x.PropertyName == "file");
    }

    [Fact]
    public void ImportBrokerReportRequestValidator_AcceptsNonEmptyFile_WithinLimit()
    {
        var validator = new ImportBrokerReportRequestValidator(
            Options.Create(new ImportOptions
            {
                MaxFileSizeMb = 1
            }));
        var file = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes("ok")), 0, 2, "file", "report.xlsx");

        var result = validator.Validate(new ImportBrokerReportRequest { File = file });

        Assert.DoesNotContain(result.Errors, x => x.PropertyName == "file");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Larchik.API.Tests";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
