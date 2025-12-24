using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrdersService.Common;
using OrdersService.Data;
using OrdersService.Realtime;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrdersService.Messaging;

public sealed class PaymentResultConsumer(
    IServiceScopeFactory scopeFactory,
    RabbitMqConnectionProvider connectionProvider,
    OrderNotifier notifier,
    ILogger<PaymentResultConsumer> logger) : BackgroundService
{
    private const string ExchangeName = "gozon.events";
    private const string QueueName = "orders.payment-results";
    private const string RoutingKey = "payment.result";

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
                var payload = JsonSerializer.Deserialize<PaymentResult>(json) ?? throw new InvalidOperationException("Invalid PaymentResult");

                await Handle(payload, stoppingToken);

                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PaymentResultConsumer failed");
                channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private async Task Handle(PaymentResult msg, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO inbox_messages (id, received_at)
            VALUES ({msg.MessageId}, {DateTimeOffset.UtcNow})
            ON CONFLICT (id) DO NOTHING
        ", ct);

        if (inserted == 0) return;

        var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == msg.OrderId, ct);
        if (order is null) return;

        order.Status = msg.Success ? OrderStatus.FINISHED : OrderStatus.CANCELLED;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await notifier.NotifyOrderStatusChanged(order.UserId, order.Id, order.Status.ToString(), ct);
    }
}
