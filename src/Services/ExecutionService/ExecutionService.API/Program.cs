using ExecutionService.API.Consumers;
using ExecutionService.API.Engine;
using ExecutionService.API.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Shared.Messaging;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
builder.Services.AddDbContext<ExecutionDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Redis (workflow definition cache) ---
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ExecutionService:";
});

// --- HTTP client to Workflow Service (for fetching definitions) ---
builder.Services.AddHttpClient<WorkflowDefinitionClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:WorkflowService"]!);
});

// --- HTTP client used by "http" node type to call arbitrary external URLs ---
builder.Services.AddHttpClient("generic-http-node", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// --- Engine ---
builder.Services.AddScoped<WorkflowExecutionEngine>();

// --- Messaging: publishes + consumes events ---
builder.Services.AddRabbitMqMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<WorkflowTriggeredConsumer>();
    busConfig.AddConsumer<AiTaskCompletedConsumer>();
    busConfig.AddConsumer<NotificationCompletedConsumer>();
});

// --- JWT Auth (shared across all services) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSharedJwtAuthentication(builder.Configuration);

// --- Controllers + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Execution Service API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExecutionDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
