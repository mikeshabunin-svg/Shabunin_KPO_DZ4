using Microsoft.EntityFrameworkCore;

namespace OrdersService.Data;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OutboxMessageEntity> Outbox => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> Inbox => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderEntity>().ToTable("orders").HasKey(x => x.Id);
        modelBuilder.Entity<OrderEntity>().HasIndex(x => new { x.UserId, x.CreatedAt });

        modelBuilder.Entity<OutboxMessageEntity>().ToTable("outbox_messages").HasKey(x => x.Id);
        modelBuilder.Entity<OutboxMessageEntity>().HasIndex(x => x.PublishedAt);

        modelBuilder.Entity<InboxMessageEntity>().ToTable("inbox_messages").HasKey(x => x.Id);
    }
}
