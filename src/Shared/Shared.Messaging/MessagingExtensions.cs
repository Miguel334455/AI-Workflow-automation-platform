using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Registers MassTransit with the RabbitMQ transport using configuration
    /// from the "RabbitMq" section. Pass a delegate to register
    /// service-specific consumers.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                       ?? new RabbitMqOptions();

        services.AddMassTransit(busConfig =>
        {
            configureConsumers?.Invoke(busConfig);

            busConfig.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                // Conventionally map each consumer to a queue named after the service + event.
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
