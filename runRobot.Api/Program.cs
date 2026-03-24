using runRobot.Api.Auth;
using runRobot.Api.Endpoints;
using runRobot.Api.Jobs;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AnalysisJobStore>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<ApiKeyMiddleware>();
AnalyzeEndpoints.Map(app);

app.Run();

// Exposed so WebApplicationFactory<Program> can reference this assembly from tests.
public partial class Program { }
