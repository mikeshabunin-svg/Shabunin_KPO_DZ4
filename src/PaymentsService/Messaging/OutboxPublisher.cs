using System.Text;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using RabbitMQ.Client;

namespace PaymentsService.Messaging;

public sealed class OutboxPublisher(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connectionProvider,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private const string ExchangeName = "gozon.events";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var ch = connectionProvider.GetConnection().CreateModel())
        {
            ch.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Payments OutboxPublisher iteration failed");
            }

            await Task.Delay(500, stoppingToken);
        }
    }

    private async Task PublishOnce(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        var now = DateTimeOffset.UtcNow;

        var batch = await db.Outbox
            .Where(x => x.PublishedAt == null && (x.LockedUntil == null || x.LockedUntil < now))
            .OrderBy(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        var lockUntil = now.AddSeconds(10);
        foreach (var msg in batch)
        {
            msg.LockedUntil = lockUntil;
            msg.Attempt += 1;
        }
        await db.SaveChangesAsync(ct);

        using var channel = connectionProvider.GetConnection().CreateModel();
        channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false);

        foreach (var msg in batch)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(msg.PayloadJson);

                var props = channel.CreateBasicProperties();
                props.Persistent = true;
                props.MessageId = msg.Id.ToString();
                props.Type = msg.Type;

                var routingKey = msg.Type switch
                {
                    "PaymentResult" => "payment.result",
                    _ => "unknown"
                };

                channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body);

                msg.PublishedAt = DateTimeOffset.UtcNow;
                msg.LockedUntil = null;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish payments outbox message {Id}", msg.Id);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
