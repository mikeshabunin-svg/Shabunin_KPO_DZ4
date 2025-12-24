namespace OrdersService.Common;

public sealed record PaymentRequested(
    Guid MessageId,
    Guid OrderId,
    string UserId,
    long Price,
    DateTimeOffset CreatedAt);

public sealed record PaymentResult(
    Guid MessageId,
    Guid OrderId,
    string UserId,
    bool Success,
    string? Reason,
    DateTimeOffset CreatedAt);
