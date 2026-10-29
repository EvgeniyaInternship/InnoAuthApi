using AuthApi.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGlobalExceptionHandler();
builder.Services.AddApiControllers();
builder.Services.AddSwaggerWithJwtAuth();
builder.Services.AddKeycloakAuthentication(builder.Configuration);
builder.Services.AddAuthServices(builder.Configuration);
builder.Services.AddApplicationServices();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwaggerDocumentation();
app.UseSecurityPipeline();
app.MapControllers();

app.Run();