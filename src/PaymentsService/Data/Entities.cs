namespace PaymentsService.Data;

public sealed class AccountEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public long Balance { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PaymentEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string UserId { get; set; } = "";
    public long Price { get; set; }
    public bool Success { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
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
