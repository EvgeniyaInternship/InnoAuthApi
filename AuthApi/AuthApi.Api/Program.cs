using AuthApi.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSwaggerWithJwtAuth();
builder.Services.AddKeycloakAuthentication(builder.Configuration);

var app = builder.Build();

app.UseSwaggerDocumentation();

app.UseSecurityPipeline();

app.MapControllers();

app.Run();