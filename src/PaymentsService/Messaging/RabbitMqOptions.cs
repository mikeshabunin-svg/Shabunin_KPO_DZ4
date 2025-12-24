namespace PaymentsService.Messaging;

public sealed class RabbitMqOptions
{
    public required string Host { get; init; }
    public required string User { get; init; }
    public required string Pass { get; init; }
}
