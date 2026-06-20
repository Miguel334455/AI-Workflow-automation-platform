using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Quartz;
using Shared.Messaging;
using TriggerService.API.Infrastructure;
using TriggerService.API.Jobs;
using TriggerService.API.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
builder.Services.AddDbContext<TriggerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Messaging (publishes WorkflowTriggeredEvent) ---
builder.Services.AddRabbitMqMessaging(builder.Configuration);

// --- Domain services ---
builder.Services.AddScoped<TriggerPublisher>();

// --- Quartz scheduler for cron-based triggers ---
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("ScheduledTriggerPollingJob");
    q.AddJob<ScheduledTriggerPollingJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("ScheduledTriggerPollingJob-trigger")
        .WithCronSchedule("0 * * * * ?")); // every minute
});
builder.Services.AddQuartzHostedService(opts => opts.WaitForJobsToComplete = true);

// --- JWT Auth (shared across all services) ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSharedJwtAuthentication(builder.Configuration);

// --- Controllers + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Trigger Service API", Version = "v1" });

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
    var db = scope.ServiceProvider.GetRequiredService<TriggerDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
