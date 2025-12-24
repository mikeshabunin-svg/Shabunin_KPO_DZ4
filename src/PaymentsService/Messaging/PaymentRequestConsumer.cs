using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Common;
using PaymentsService.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentsService.Messaging;

public sealed class PaymentRequestConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connectionProvider,
    ILogger<PaymentRequestConsumer> logger) : BackgroundService
{
    private const string ExchangeName = "gozon.events";
    private const string QueueName = "payments.payment-requests";
    private const string RoutingKey = "payment.requested";

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var conn = connectionProvider.GetConnection();
        var channel = conn.CreateModel();

        channel.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Topic, durable: true, autoDelete: false);
        channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(queue: QueueName, exchange: ExchangeName, routingKey: RoutingKey);

        channel.BasicQos(0, 16, false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var payload = JsonSerializer.Deserialize<PaymentRequested>(json) ?? throw new InvalidOperationException("Invalid PaymentRequested");

                await Handle(payload, stoppingToken);

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PaymentRequestConsumer failed");
                channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private async Task Handle(PaymentRequested msg, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO inbox_messages (id, received_at)
            VALUES ({msg.MessageId}, {DateTimeOffset.UtcNow})
            ON CONFLICT (id) DO NOTHING
        ", ct);

        if (inserted == 0)
        {
            await tx.CommitAsync(ct);
            return;
        }

        // If already processed by orderId (unique index) - idempotency
        var already = await db.Payments.AnyAsync(p => p.OrderId == msg.OrderId, ct);
        if (already)
        {
            await tx.CommitAsync(ct);
            return;
        }

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == msg.UserId, ct);

        bool success;
        string? reason = null;

        if (account is null)
        {
            success = false;
            reason = "Account not found";
        }
        else if (account.Balance < msg.Price)
        {
            success = false;
            reason = "Insufficient funds";
        }
        else
        {
            account.Balance -= msg.Price;
            success = true;
        }

        db.Payments.Add(new PaymentEntity
        {
            OrderId = msg.OrderId,
            UserId = msg.UserId,
            Price = msg.Price,
            Success = success,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var result = new PaymentResult(Guid.NewGuid(), msg.OrderId, msg.UserId, success, reason, DateTimeOffset.UtcNow);

        db.Outbox.Add(new OutboxMessageEntity
        {
            Type = "PaymentResult",
            PayloadJson = JsonSerializer.Serialize(result),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
