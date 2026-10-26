using AuthApi.Api.Configuration;
using AuthApi.Api.Extensions;
using AuthApi.Api.Interfaces;
using AuthApi.Api.Middlewares;
using AuthApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();

builder.Services.AddSwaggerWithJwtAuth();
builder.Services.AddKeycloakAuthentication(builder.Configuration);

builder.Services.Configure<KeycloakOptions>(
    builder.Configuration.GetSection(KeycloakOptions.SectionName));

builder.Services.AddHttpClient<IAuthService, AuthService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSwaggerDocumentation();

app.UseSecurityPipeline();

app.MapControllers();

app.Run();