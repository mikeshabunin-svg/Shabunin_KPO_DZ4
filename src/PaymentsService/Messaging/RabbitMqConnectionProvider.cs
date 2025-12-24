using RabbitMQ.Client;

namespace PaymentsService.Messaging;

public sealed class RabbitMqConnectionProvider(RabbitMqOptions options) : IDisposable
{
    private IConnection? _conn;

    public IConnection GetConnection()
    {
        if (_conn is { IsOpen: true }) return _conn;

        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            UserName = options.User,
            Password = options.Pass,
            DispatchConsumersAsync = true
        };

        _conn = factory.CreateConnection("payments-service");
        return _conn;
    }

    public void Dispose()
    {
        try { _conn?.Dispose(); } catch { }
    }
}
