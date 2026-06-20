using AITaskService.API.Consumers;
using AITaskService.API.Services;
using Microsoft.OpenApi.Models;
using Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

// --- AI provider client ---
builder.Services.AddHttpClient<IAiClient, AiClient>();

// --- Messaging: consumes AITaskRequestedEvent, publishes AITaskCompletedEvent ---
builder.Services.AddRabbitMqMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<AiTaskRequestedConsumer>();
});

// --- JWT Auth (shared across all services; service exposes minimal HTTP surface) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSharedJwtAuthentication(builder.Configuration);

// --- Controllers + Swagger (mainly for health checks) ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AI Task Service API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();
