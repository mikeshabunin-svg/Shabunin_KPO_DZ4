using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Messaging;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration["CONNECTION_STRING"] ?? throw new InvalidOperationException("CONNECTION_STRING is required");
builder.Services.AddDbContext<PaymentsDbContext>(o => o.UseNpgsql(cs));

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

builder.Services.AddHostedService<PaymentRequestConsumer>();
builder.Services.AddHostedService<OutboxPublisher>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
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

app.UseSwagger(c => { c.RouteTemplate = "payments/swagger/{documentName}/swagger.json"; });
app.UseSwaggerUI(c => { c.RoutePrefix = "payments/swagger"; c.SwaggerEndpoint("v1/swagger.json", "PaymentsService v1"); });

app.MapPost("/payments/api/account", async (PaymentsDbContext db, CreateAccountRequest body) =>
{
    var userId = body.UserId ?? throw new InvalidOperationException("userId required");

    var exists = await db.Accounts.AnyAsync(x => x.UserId == userId);
    if (exists) return Results.Conflict(new { error = "Account already exists" });

    db.Accounts.Add(new AccountEntity { UserId = userId, Balance = 0 });
    await db.SaveChangesAsync();

    return Results.Ok(new { userId });
})
.Accepts<CreateAccountRequest>("application/json")
.WithOpenApi();

app.MapPost("/payments/api/account/topup", async (PaymentsDbContext db, TopUpRequest body) =>
{
    var userId = body.UserId ?? throw new InvalidOperationException("userId required");
    var amount = body.Amount;

    var account = await db.Accounts.FirstOrDefaultAsync(x => x.UserId == userId);
    if (account is null) return Results.NotFound(new { error = "Account not found" });

    account.Balance += amount;
    await db.SaveChangesAsync();

    return Results.Ok(new { userId, balance = account.Balance });
})
.Accepts<TopUpRequest>("application/json")
.WithOpenApi();

app.MapGet("/payments/api/account/balance", async (PaymentsDbContext db, string userId) =>
{
    var account = await db.Accounts.FirstOrDefaultAsync(x => x.UserId == userId);
    if (account is null) return Results.NotFound(new { error = "Account not found" });

    return Results.Ok(new { userId, balance = account.Balance });
});

app.Run();


public sealed record CreateAccountRequest(string UserId);
public sealed record TopUpRequest(string UserId, long Amount);
