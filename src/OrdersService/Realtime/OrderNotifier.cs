using Microsoft.AspNetCore.SignalR;

namespace OrdersService.Realtime;

public sealed class OrderNotifier(IHubContext<OrdersHub> hub)
{
    public Task NotifyOrderStatusChanged(string userId, Guid orderId, string status, CancellationToken ct)
    {
        return hub.Clients.Group($"user:{userId}")
            .SendAsync("OrderStatusChanged", new { orderId, status }, cancellationToken: ct);
    }
}
