namespace AutoGrading.Common.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "autograding.events";

    /// <summary>Used as the queue-name prefix for this service's subscriptions.</summary>
    public string ServiceName { get; set; } = "service";
}
