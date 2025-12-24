using Microsoft.AspNetCore.SignalR;

namespace OrdersService.Realtime;

public sealed class OrdersHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var userId = http?.Request.Query["userId"].ToString();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnConnectedAsync();
    }
}
