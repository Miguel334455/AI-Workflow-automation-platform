using Microsoft.OpenApi.Models;
using NotificationService.API.Consumers;
using NotificationService.API.Services;
using Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

// --- Notification senders (Email/Slack/Webhook) ---
builder.Services.AddHttpClient<INotificationSender, NotificationSender>();

// --- Messaging: consumes NotificationRequestedEvent, publishes NotificationCompletedEvent ---
builder.Services.AddRabbitMqMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<NotificationRequestedConsumer>();
});

// --- JWT Auth (shared across all services; service exposes minimal HTTP surface) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSharedJwtAuthentication(builder.Configuration);

// --- Controllers + Swagger (mainly for health checks) ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.Run();
