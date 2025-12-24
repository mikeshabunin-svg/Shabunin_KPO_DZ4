using Microsoft.EntityFrameworkCore;

namespace PaymentsService.Data;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<OutboxMessageEntity> Outbox => Set<OutboxMessageEntity>();
    public DbSet<InboxMessageEntity> Inbox => Set<InboxMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>().ToTable("accounts").HasKey(x => x.Id);
        modelBuilder.Entity<AccountEntity>().HasIndex(x => x.UserId).IsUnique();

        modelBuilder.Entity<PaymentEntity>().ToTable("payments").HasKey(x => x.Id);
        modelBuilder.Entity<PaymentEntity>().HasIndex(x => x.OrderId).IsUnique();

        modelBuilder.Entity<OutboxMessageEntity>().ToTable("outbox_messages").HasKey(x => x.Id);
        modelBuilder.Entity<InboxMessageEntity>().ToTable("inbox_messages").HasKey(x => x.Id);
    }
}
