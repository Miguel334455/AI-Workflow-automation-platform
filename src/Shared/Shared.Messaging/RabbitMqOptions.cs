namespace Shared.Messaging;

/// <summary>
/// Bound from the "RabbitMq" section of appsettings.json / environment variables
/// in each service.
/// </summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "rabbitmq";
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
