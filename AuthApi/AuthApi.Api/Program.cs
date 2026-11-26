using AuthApi.Api.Extensions;
using Microsoft.AspNetCore.HttpLogging;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
    logging.CombineLogs = true;
});
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddApiControllers();
builder.Services.AddSwaggerWithJwtAuth();
builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthServices(builder.Configuration);
builder.Services.AddApplicationServices();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseHttpLogging();
app.UseExceptionHandler();
app.UseSwaggerDocumentation();
app.UseSecurityPipeline();
app.MapControllers();

Log.Information("Auth API started.");
app.Run();