using Larchik.API.Extensions;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddSecurityServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);


var app = builder.Build();


await app.RunAsync();