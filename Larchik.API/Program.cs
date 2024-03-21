using Larchik.API.Extensions;
using Larchik.API.Middleware;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerServices();
builder.Services.AddSecurityServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddCorsServices(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using var scope = app.Services.CreateScope();

var services = scope.ServiceProvider;

try
{
    var context = services.GetRequiredService<LarchikContext>();
    await context.Database.MigrateAsync();
}
catch (Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "An error occurred during migration or seed");
}

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Larchik API v2.0");
    });
}
app.UseHttpsRedirection();

app.UseCors("CorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();