using Microsoft.EntityFrameworkCore;

namespace OrdersService.Data;

public enum OrderStatus
{
    NEW = 0,
    FINISHED = 1,
    CANCELLED = 2
}

public sealed class OrderEntity
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public long Price { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.NEW;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempt { get; set; }
}

public sealed class InboxMessageEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
