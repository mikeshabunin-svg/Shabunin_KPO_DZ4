using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrdersService.Common;
using OrdersService.Data;
using OrdersService.Messaging;
using OrdersService.Realtime;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration["CONNECTION_STRING"] ?? throw new InvalidOperationException("CONNECTION_STRING is required");
builder.Services.AddDbContext<OrdersDbContext>(o => o.UseNpgsql(cs));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rabbit = new RabbitMqOptions
{
    Host = builder.Configuration["RABBITMQ_HOST"] ?? "localhost",
    User = builder.Configuration["RABBITMQ_USER"] ?? "guest",
    Pass = builder.Configuration["RABBITMQ_PASS"] ?? "guest"
};
builder.Services.AddSingleton(rabbit);
builder.Services.AddSingleton<RabbitMqConnectionProvider>();

builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<PaymentResultConsumer>();

var redis = builder.Configuration["REDIS_CONNECTION"];
var signalr = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(redis))
{
    signalr.AddStackExchangeRedis(redis);
}
builder.Services.AddSingleton<OrderNotifier>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
    for (var i = 0; i < 30; i++)
    {
        try
        {
            await db.Database.EnsureCreatedAsync();
            break;
        }
        catch (Exception) when (i < 29)
        {
            await Task.Delay(1000);
        }
    }
}

app.UseSwagger(c => { c.RouteTemplate = "orders/swagger/{documentName}/swagger.json"; });
app.UseSwaggerUI(c => { c.RoutePrefix = "orders/swagger"; c.SwaggerEndpoint("v1/swagger.json", "OrdersService v1"); });

app.MapHub<OrdersHub>("/orders/ws/orders");

app.MapPost("/orders/api/orders", async (OrdersDbContext db, CreateOrderRequest body) =>
{
    var userId = body.UserId ?? throw new InvalidOperationException("userId required");
    var price = body.Price;

    var order = new OrderEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Price = price,
        Status = OrderStatus.NEW,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    await using var tx = await db.Database.BeginTransactionAsync();

    db.Orders.Add(order);

    var evt = new PaymentRequested(Guid.NewGuid(), order.Id, order.UserId, order.Price, DateTimeOffset.UtcNow);

    db.Outbox.Add(new OutboxMessageEntity
    {
        Type = "PaymentRequested",
        PayloadJson = JsonSerializer.Serialize(evt),
        CreatedAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync();
    await tx.CommitAsync();

    return Results.Ok(new { orderId = order.Id, status = order.Status.ToString() });
})
.Accepts<CreateOrderRequest>("application/json")
.WithOpenApi();

app.MapGet("/orders/api/orders", async (OrdersDbContext db, string userId) =>
{
    var list = await db.Orders
        .Where(x => x.UserId == userId)
        .OrderByDescending(x => x.CreatedAt)
        .Select(x => new { id = x.Id, userId = x.UserId, price = x.Price, status = x.Status.ToString(), createdAt = x.CreatedAt })
        .ToListAsync();

    return Results.Ok(list);
});

app.MapGet("/orders/api/orders/{id:guid}", async (OrdersDbContext db, Guid id) =>
{
    var order = await db.Orders.FirstOrDefaultAsync(x => x.Id == id);
    if (order is null) return Results.NotFound();

    return Results.Ok(new { id = order.Id, userId = order.UserId, price = order.Price, status = order.Status.ToString(), createdAt = order.CreatedAt });
});

app.Run();


public sealed record CreateOrderRequest(string UserId, long Price);
