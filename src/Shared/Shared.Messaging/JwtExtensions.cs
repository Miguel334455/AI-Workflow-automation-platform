using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Messaging;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "WorkflowPlatform";
    public string Audience { get; set; } = "WorkflowPlatformClients";

    /// <summary>
    /// Symmetric signing key shared by all services. In production this
    /// should come from a secret store, not appsettings.
    /// </summary>
    public string SigningKey { get; set; } = "CHANGE_ME_super_secret_signing_key_min_32_chars";
}

public static class JwtExtensions
{
    /// <summary>
    /// Adds JWT bearer authentication using a shared symmetric signing key.
    /// All services use the same configuration so a token issued by the
    /// Auth/Workflow service is valid everywhere.
    /// </summary>
    public static IServiceCollection AddSharedJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                          ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
